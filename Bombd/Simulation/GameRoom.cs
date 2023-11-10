using Bombd.Helpers;
using Bombd.Types.GameBrowser;
using Bombd.Types.GameManager;
using Bombd.Types.Requests;

namespace Bombd.Simulation;

public class GameRoom
{
    private readonly Dictionary<int, GamePlayer> _playerIdLookup = new();
    private readonly List<bool> _slots;
    private readonly Dictionary<int, GamePlayer> _userIdLookup = new();

    public readonly GameManagerGame Game;
    public readonly GameSimulation Simulation;

    private int _gameCreationTime = TimeHelper.LocalTime;
    private int _lastPlayerJoinTime = TimeHelper.LocalTime;
    
    public GameRoom(RoomCreationInfo request)
    {
        Game = request.Game;
        Platform = request.Platform;
        Simulation = new GameSimulation(
            request.Type, request.Platform, request.OwnerUserId, Game.Players);
        _slots = Enumerable.Repeat(false, request.MaxSlots).ToList();
        MaxSlots = request.MaxSlots;
        NumFreeSlots = request.MaxSlots;
    }

    public readonly Platform Platform;
    public readonly int MaxSlots;
    public int NumFreeSlots { get; private set; }
    public int UsedSlots => MaxSlots - NumFreeSlots;
    
    public GamePlayer? TryJoin(string username, int userId)
    {
        if (NumFreeSlots == 0) return null;
        if (!RequestSlot(out int playerId)) return null;
        return Join(username, userId, playerId);
    }

    public GamePlayer Join(string username, int userId, int playerId)
    {
        var player = new GamePlayer
        {
            Platform = Platform,
            PlayerId = playerId,
            UserId = userId,
            Username = username,
            Room = this
        };

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

    public bool RequestSlot(out int playerId)
    {
        playerId = 0;
        if (NumFreeSlots == 0) return false;

        int index = _slots.FindIndex(x => !x);
        if (index == -1) return false;

        _lastPlayerJoinTime = TimeHelper.LocalTime;
        _slots[index] = true;
        NumFreeSlots--;

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
            _slots[slotId] = false;
            NumFreeSlots++;
            return true;
        }

        return false;
    }

    public GamePlayer GetPlayerByUserId(int userId) => _userIdLookup[userId];
    public GamePlayer GetPlayerByPlayerId(int playerId) => _playerIdLookup[playerId];

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