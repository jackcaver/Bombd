using System.Collections.Concurrent;
using Bombd.Helpers;
using Bombd.Simulation;
using Bombd.Types.GameBrowser;
using Bombd.Types.GameManager;
using Bombd.Types.Requests;

namespace Bombd.Core;

public class RoomManager
{
    private readonly ConcurrentDictionary<int, GameRoom> _roomIds = new();
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly ConcurrentDictionary<int, GameRoom> _userRooms = new();
    private readonly ConcurrentDictionary<int, int> _creationPlayerCounts = new();
    private readonly SemaphoreSlim _playerCountLock = new(1);
    
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
    
    public GamePlayer? TryJoinRoom(string username, int userId, GameRoom room)
    {
        GamePlayer? player = room.TryJoin(username, userId);
        if (player == null) return null;
        IncrementPlayerCount(room);
        _userRooms[player.UserId] = room;
        return player;
    }

    public GamePlayer JoinRoom(string username, int userId, int playerId, GameRoom room)
    {
        GamePlayer player = room.Join(username, userId, playerId);
        IncrementPlayerCount(room);
        _userRooms[player.UserId] = room;
        return player;
    }

    public bool TryLeaveCurrentRoom(int userId)
    {
        GameRoom? room = GetRoomByUser(userId);
        if (room == null) return false;
        
        DecrementPlayerCount(room);
        _userRooms.Remove(userId, out _);

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
        else if (request.Platform == Platform.Karting)
            type = ServerType.Pod;
        
        int id = ++_nextGameId;
        string name = $"gm_{request.Attributes["SERVER_TYPE"].ToLower()}_{id}";
        var game = new GameManagerGame
        {
            GameName = name,
            GameBrowserName = name,
            GameId = id,
            Players = new GameManagerPlayerList(),
            Attributes = request.Attributes
        };
        var room = new GameRoom(game, type, request.Platform, maxSlots, request.OwnerUserId);
        
        _rooms[name] = room;
        _roomIds[id] = room;

        return room;
    }
    
    public GameBrowserBusiestList GetBusiestCreations()
    {
        var busiest = new GameBrowserBusiestList();
        
        _playerCountLock.Wait();
        
        var creations = _creationPlayerCounts.ToList();
        creations.Sort((z, a) => a.Value.CompareTo(z.Value));
        foreach (var creation in creations)
        {
            if (busiest.Count == GameBrowserBusiestList.MaxSize) break;
            if (creation.Value == 0) continue;
            busiest.Add(creation.Key);
        }
        
        _playerCountLock.Wait();
        return busiest;
    }

    public void FillCreationPlayerCounts(GamePlayerCounts counts)
    {
        _playerCountLock.Wait();
        foreach (int key in counts.Data.Keys.Where(key => _creationPlayerCounts.ContainsKey(key)))
        {
            counts.Data[key] = _creationPlayerCounts[key];
        }
        _playerCountLock.Release();
    }

    private void IncrementPlayerCount(GameRoom room)
    {
        _playerCountLock.Wait();
        if (room.Game.Attributes.TryGetValue("TRACK_CREATIONID", out string? attribute))
        {
            if (!int.TryParse(attribute, out int creationId)) return;
            if (!_creationPlayerCounts.TryAdd(creationId, 1)) 
                _creationPlayerCounts[creationId] += 1;
        }
        _playerCountLock.Release();
    }
    
    private void DecrementPlayerCount(GameRoom room)
    {
        _playerCountLock.Wait();
        if (room.Game.Attributes.TryGetValue("TRACK_CREATIONID", out string? attribute))
        {
            if (!int.TryParse(attribute, out int creationId)) return;
            if (_creationPlayerCounts.TryGetValue(creationId, out int playerCount))
                _creationPlayerCounts[creationId] = playerCount - 1;
        }
        _playerCountLock.Release();
    }
    
    public List<GameBrowserGame> SearchRooms(GameAttributes attributes, Platform id, bool createIfNoneExist = true)
    {
        List<GameRoom> rooms = new List<GameRoom>(_rooms.Values).Where(room =>
        {
            if (room.Platform != id) return false;
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