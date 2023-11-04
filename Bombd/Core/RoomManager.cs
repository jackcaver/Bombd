using Bombd.Helpers;
using Bombd.Simulation;
using Bombd.Types.GameBrowser;
using Bombd.Types.GameManager;
using Bombd.Types.Requests;

namespace Bombd.Core;

public class RoomManager
{
    private readonly Dictionary<int, GameRoom> _roomIds = new();
    private readonly Dictionary<string, GameRoom> _rooms = new();
    private readonly Dictionary<int, GameRoom> _userRooms = new();
    private int _nextGameId;

    public List<GameRoom> GetRooms() => new(_rooms.Values);

    public GameRoom? GetRoomByUser(int userId)
    {
        if (_userRooms.TryGetValue(userId, out GameRoom? room)) return room;
        return null;
    }

    public GameRoom? GetRoomByName(string name)
    {
        if (_rooms.TryGetValue(name, out GameRoom? room)) return room;
        return null;
    }

    public GamePlayer? GetPlayerInRoom(int userId)
    {
        GameRoom? room = GetRoomByUser(userId);
        return room?.GetPlayerByUserId(userId);
    }

    public GamePlayer? GetPlayerById(int playerId)
    {
        if (_roomIds.TryGetValue(playerId >>> 8, out GameRoom? room)) return room.GetPlayerByPlayerId(playerId);

        return null;
    }

    public GamePlayer? TryJoinRoom(string username, int userId, GameRoom room)
    {
        GamePlayer? player = room.TryJoin(username, userId);
        if (player == null) return null;

        _userRooms[player.UserId] = room;
        return player;
    }

    public GamePlayer JoinRoom(string username, int userId, int playerId, GameRoom room)
    {
        GamePlayer player = room.Join(username, userId, playerId);
        _userRooms[player.UserId] = room;
        return player;
    }

    public bool TryLeaveCurrentRoom(int userId)
    {
        GameRoom? room = GetRoomByUser(userId);
        if (room == null) return false;

        _userRooms.Remove(userId);

        GamePlayer player = room.GetPlayerByUserId(userId);
        return room.TryLeave(player.PlayerId);
    }

    public GameRoom CreateRoom(CreateGameRequest request)
    {
        // Add default attributes to games if they weren't
        // sent by the game for whatever reason.
        request.Attributes.TryAdd("__IS_RANKED", "0");
        request.Attributes.TryAdd("__JOIN_MODE", "OPEN");
        request.Attributes.TryAdd("__MM_MODE_G", "OPEN");
        request.Attributes.TryAdd("__MAX_PLAYERS", "8");
        request.Attributes.TryAdd("SERVER_TYPE", "kartPark");
        request.Attributes.TryAdd("COMM_CHECKSUM", ((int)request.Platform).ToString());

        // If we can't parse the number of players, just default to an existing number.
        if (!int.TryParse(request.Attributes["__MAX_PLAYERS"], out int maxSlots))
            maxSlots = 8;

        var type = ServerType.KartPark;
        if (request.Attributes["SERVER_TYPE"] == "competitive")
            type = ServerType.Competitive;

        int id = ++_nextGameId;
        string name = "gm_zone_" + (uint)id;
        var room = new GameRoom(new GameManagerGame
        {
            GameName = name,
            GameBrowserName = name,
            GameId = id,
            Players = new GameManagerPlayerList(),
            Attributes = request.Attributes
        }, maxSlots, type, request.Platform, request.OwnerUserId);

        _rooms[name] = room;
        _roomIds[id] = room;

        return room;
    }

    public Dictionary<int, int> GetCreationPlayerCounts(Platform platform)
    {
        IEnumerable<GameRoom> rooms = new List<GameRoom>(_rooms.Values).Where(
            room => room.Game.Attributes.ContainsKey("TRACK_CREATIONID"));

        Dictionary<int, int> counts = new();
        foreach (GameRoom room in rooms)
        {
            int creationId = int.Parse(room.Game.Attributes["TRACK_CREATIONID"]);
            if (!counts.TryAdd(creationId, room.UsedSlots)) counts[creationId] += room.UsedSlots;
        }

        return counts;
    }

    public List<GameBrowserGame> SearchRooms(GameAttributes attributes, Platform id, bool createIfNoneExist = true)
    {
        List<GameRoom> rooms = new List<GameRoom>(_rooms.Values).Where(room =>
        {
            if (room.NumFreeSlots == 0) return false;
            foreach (KeyValuePair<string, string> attribute in attributes)
            {
                if (attribute.Value == "_ANY") continue;
                if (!room.Game.Attributes.TryGetValue(attribute.Key, out string? value)) return false;
                if (value != attribute.Value)
                    return false;
            }

            return true;
        }).ToList();

        if (rooms.Count == 0 && createIfNoneExist)
            rooms.Add(CreateRoom(new CreateGameRequest { Attributes = attributes, Platform = id }));

        return rooms.Select(room => room.ToGameBrowser()).ToList();
    }
}