using Bombd.Helpers;
using Bombd.Types.GameBrowser;
using Bombd.Types.GameManager;
using Bombd.Types.Requests;

namespace Bombd.Simulation;

public class GameRoom
{
    private const int MaxSlots = 12;
    
    private readonly Dictionary<int, GamePlayer> _playerIdLookup = new();
    private readonly List<bool> _slots;
    private readonly List<int> _slotGuestCounts;
    private readonly Dictionary<int, GamePlayer> _userIdLookup = new();
    
    public readonly GameManagerGame Game;
    public readonly GameSimulation Simulation;
    
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
        Simulation = new GameSimulation(
            request.Type, request.Platform, request.OwnerUserId, Game.Players);
        _slots = Enumerable.Repeat(false, MaxSlots).ToList();
        _slotGuestCounts = Enumerable.Repeat(0, MaxSlots).ToList();
        MaxPlayers = request.MaxPlayers;
        UsedSlots = 0;
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
        var player = new GamePlayer
        {
            Platform = Platform,
            PlayerId = playerId,
            UserId = userId,
            Username = username,
            Room = this
        };

        if (guests != null)
        {
            foreach (var guest in guests)
                player.Guests.Add(new GameGuest(guest));
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
        return Simulation.IsHostReady();
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