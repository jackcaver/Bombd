using Bombd.Core;
using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Types.GameBrowser;
using Bombd.Types.GameManager;
using Bombd.Types.Network.Objects;
using Bombd.Types.Network.Races;
using Bombd.Types.Network.Room;
using Bombd.Types.Requests;

namespace Bombd.Types.Network.Simulation;

public class GameRoom
{
    private const int MaxSlots = 12;
    
    private readonly Dictionary<int, GamePlayer> _playerIdLookup = new();
    private readonly List<bool> _slots;
    private readonly List<int> _slotGuestCounts;
    private readonly Dictionary<int, GamePlayer> _userIdLookup = new();
    
    public readonly GameManagerGame Game;
    public readonly SimServer Simulation;
    
    public int Owner => Simulation.Owner;
    
    public bool IsEmpty => UsedSlots == 0;
    public bool IsFull => UsedSlots == MaxPlayers;
    public int NumFreeSlots => MaxPlayers - UsedSlots;
    public int UsedSlots { get; private set; }
    
    public readonly Platform Platform;
    public int MaxPlayers;
    
    private int _gameCreationTime = TimeHelper.LocalTime;
    private int _lastPlayerJoinTime = TimeHelper.LocalTime;
    
    public GameRoom(RoomCreationInfo request)
    {
        Game = request.Game;
        Platform = request.Platform;
        Simulation = new SimServer(
            request.Type, this, request.OwnerUserId, request.IsRanked, request.IsSeries);
        _slots = Enumerable.Repeat(false, MaxSlots).ToList();
        _slotGuestCounts = Enumerable.Repeat(0, MaxSlots).ToList();
        MaxPlayers = request.MaxPlayers;
        UsedSlots = 0;
    }

    public void UpdateAttributes(EventSettings settings)
    {
        var attr = Game.Attributes;

        settings.Print();

        string visibility = settings.Private ? "CLOSED" : "OPEN";
        attr["__MM_MODE_G"] = visibility;
        attr["__JOIN_MODE"] = visibility;

        MaxPlayers = settings.MaxHumans;
        attr["__MAX_PLAYERS"] = MaxPlayers.ToString();
        
        // TODO: Adjust player counts on Karting?
        // Switching tracks doesn't work yet, so it's not important right now
        
        attr["TRACK_CREATIONID"] = settings.CreationId.ToString();
        if (settings.CreationId >= NetCreationIdRange.MinOnlineCreationId)
            attr["TRACK_GROUP"] = "userCreated";
        else attr["TRACK_GROUP"] = "official";
        
        if (!string.IsNullOrEmpty(settings.KartParkHome))
            attr["KART_PARK_HOME"] = settings.KartParkHome;

        if (settings.CareerEventIndex == -1) attr.Remove("SPHERE_INDEX");
        else attr["SPHERE_INDEX"] = settings.CareerEventIndex.ToString();

        if (settings.SeriesEventIndex == -1)
        {
            attr["SERIES_TYPE"] = "single";
            attr.Remove("EVENT_INDEX");
        }
        else
        {
            attr["SERIES_TYPE"] = "series";
            attr["EVENT_INDEX"] = settings.SeriesEventIndex.ToString();
        }
        
        switch (settings.KartSpeed)
        {
            case SpeedClass.Fast:
                attr["SPEED_CLASS"] = "fast";
                break;
            case SpeedClass.Faster:
                attr["SPEED_CLASS"] = "faster";
                break;
            case SpeedClass.Fastest:
                attr["SPEED_CLASS"] = "fastest";
                break;
            default:
                attr.Remove("SPEED_CLASS");
                break;
        }

        switch (settings.RaceType)
        {
            case RaceType.Pure:
                attr["MODE_TYPE"] = "pure";
                break;
            case RaceType.Action:
                attr["MODE_TYPE"] = "action";
                break;
            case RaceType.Battle:
                attr["MODE_TYPE"] = "battle";
                break;
            default:
                attr.Remove("MODE_TYPE");
                break;
        }
        
        Logger.LogDebug<GameRoom>($"Updating ${Game.GameName} attributes:");
        foreach (var attribute in attr)
        {
            Logger.LogDebug<GameRoom>($"\t{attribute.Key} = {attribute.Value}");
        }
    }
    
    public GamePlayer? TryJoin(string username, int userId, List<string>? guests = null)
    {
        if (NumFreeSlots == 0) return null;
        int guestCount = guests?.Count ?? 0;
        if (!RequestSlotWithGuests(guestCount, out int playerId)) return null;
        return Join(username, userId, playerId, guests);
    }

    public GamePlayer Join(string username, int userId, int playerId, List<string>? guests = null)
    {
        var player = new GamePlayer(this, username, userId, playerId);
        if (guests != null)
        {
            foreach (var guest in guests)
                player.Guests.Add(new GameGuest(username, guest));
        }
        
        _playerIdLookup[playerId] = player;
        _userIdLookup[userId] = player;
        Game.Players.Add(player);
        _lastPlayerJoinTime = TimeHelper.LocalTime;

        return player;
    }

    public bool TryLeave(int playerId)
    {
        if (FreeSlot(playerId))
        {
            GamePlayer player = _playerIdLookup[playerId];
            _userIdLookup.Remove(player.UserId);
            _playerIdLookup.Remove(playerId);
            Game.Players.Remove(player);
            
            return true;
        }

        return false;
    }

    public void UpdateGuestStatuses(GamePlayer player, GuestStatusBlock block)
    {
        int gameId = player.PlayerId >>> 8;
        if (gameId != Game.GameId) return;
        int slotId = player.PlayerId & 0x3f;
        
        foreach (GuestStatus guestStatus in block)
        {
            GameGuest? guest = player.GetGuestByName(guestStatus.Username);
            switch (guestStatus.Status)
            {
                case GuestStatusCode.AttachRequest:
                {
                    // Another case where the guest shouldn't exist in game already if
                    // we're receiving an attach request, but still check for it anyway
                    if (NumFreeSlots == 0 || guest != null)
                    {
                        guestStatus.Status = GuestStatusCode.AttachFail;
                        continue;
                    }

                    UsedSlots += 1;
                    _slotGuestCounts[slotId]++;
                    player.Guests.Add(new GameGuest(player.Username, guestStatus.Username));
                    guestStatus.Status = GuestStatusCode.AttachSuccess;
                    break;
                }
                case GuestStatusCode.DetachRequest:
                {
                    // The guest should be in game if we've received a detach request,
                    // but check if its null anyway.
                    if (guest != null)
                    {
                        UsedSlots -= 1;
                        _slotGuestCounts[slotId]--;
                        player.Guests.Remove(guest);
                    }
                    
                    guestStatus.Status = GuestStatusCode.Detached;
                    break;   
                }
            }
        }
    }

    public bool RequestSlotWithGuests(int guestCount, out int playerId)
    {
        playerId = 0;
        if (NumFreeSlots <= guestCount + 1) return false;
        if (!RequestSlot(out playerId)) return false;
        UsedSlots += guestCount;
        return true;
    }

    public bool RequestSlot(out int playerId)
    {
        playerId = 0;
        if (NumFreeSlots == 0) return false;

        int index = _slots.FindIndex(x => !x);
        if (index == -1) return false;

        _lastPlayerJoinTime = TimeHelper.LocalTime;
        _slots[index] = true;
        UsedSlots++;
        
        playerId = (Game.GameId << 8) | (index & 0x3f);
        return true;
    }

    public bool FreeSlot(int slot)
    {
        int gameId = slot >>> 8;
        if (gameId != Game.GameId) return false;
        int slotId = slot & 0x3f;

        if (_slots[slotId])
        {
            UsedSlots--;
            UsedSlots -= _slotGuestCounts[slotId];
            
            _slots[slotId] = false;
            _slotGuestCounts[slotId] = 0;
            
            return true;
        }

        return false;
    }
    
    public bool IsPlayerInGame(int playerId) => _playerIdLookup.ContainsKey(playerId);
    public bool IsUserInGame(int userId) => _userIdLookup.ContainsKey(userId);
    public bool IsOwnerInGame()
    {
        return Owner == -1 || _userIdLookup.ContainsKey(Owner);
    }

    public bool IsReadyToJoin(int userId)
    {
        if (userId == Owner || Owner == -1) return true;
        return Simulation.IsRoomReady();
    }
    
    public GamePlayer GetUser(int userId) => _userIdLookup[userId];
    public GamePlayer GetPlayer(int playerId) => _playerIdLookup[playerId];

    public GameBrowserGame GetGameBrowserInfo() =>
        new()
        {
            Platform = Platform,
            TimeSinceLastPlayerJoined = TimeHelper.LocalTime - _lastPlayerJoinTime,
            Players = new GameBrowserPlayerList(Game.Players),
            GameName = Game.GameName,
            DisplayName = Game.GameName,
            Attributes = Game.Attributes,
            NumFreeSlots = NumFreeSlots
        };
}