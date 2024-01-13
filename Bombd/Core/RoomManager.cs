using System.Collections.Concurrent;
using Bombd.Helpers;
using Bombd.Simulation;
using Bombd.Types.GameBrowser;
using Bombd.Types.GameManager;
using Bombd.Types.Requests;

namespace Bombd.Core;

public class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    
    private readonly Dictionary<int, GameRoom> _userRooms = new();
    private readonly Dictionary<int, int> _creationPlayerCounts = new();
    
    private readonly object _roomLock = new();
    private readonly object _playerCountLock = new();
    private int _nextGameId;
    
    public List<GameRoom> GetRooms()
    {
        lock (_roomLock)
        {
            return new List<GameRoom>(_rooms.Values);
        }
    }

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
        return room?.GetUser(userId);
    }

    public void DestroyRoom(GameRoom room)
    {
        lock (_roomLock)
        {
            _rooms.TryRemove(room.Game.GameName, out _);
        }
    }
    
    public GamePlayer? TryJoinRoom(string username, int userId, GameRoom room, List<string>? guests = null)
    {
        GamePlayer? player = room.TryJoin(username, userId, guests);
        if (player == null) return null;
        
        if (room.Platform == Platform.Karting)
            IncrementPlayerCount(room);
        
        _userRooms[player.UserId] = room;
        return player;
    }

    public GamePlayer JoinRoom(string username, int userId, int playerId, GameRoom room, List<string>? guests = null)
    {
        GamePlayer player = room.Join(username, userId, playerId, guests);
        if (room.Platform == Platform.Karting)
            IncrementPlayerCount(room);
        _userRooms[player.UserId] = room;
        return player;
    }

    public bool TryLeaveCurrentRoom(int userId)
    {
        GameRoom? room = GetRoomByUser(userId);
        if (room == null) return false;
        
        if (room.Platform == Platform.Karting)
            DecrementPlayerCount(room);
        
        _userRooms.Remove(userId);

        GamePlayer player = room.GetUser(userId);
        return room.TryLeave(player.PlayerId);
    }

    public GameRoom CreateRoom(CreateGameRequest request)
    {
        request.Attributes["__IS_RANKED"] = request.IsRanked ? "1" : "0";
        request.Attributes["COMM_CHECKSUM"] = ((int)request.Platform).ToString();
        
        // Add default attributes to games if they weren't
        // sent by the game for whatever reason.
        request.Attributes.TryAdd("__JOIN_MODE", "OPEN");
        request.Attributes.TryAdd("__MM_MODE_G", "OPEN");
        request.Attributes.TryAdd("__MM_MODE_P", "OPEN");
        request.Attributes.TryAdd("SERVER_TYPE", "kartPark");
        
        var type = ServerType.KartPark;
        if (request.Attributes["SERVER_TYPE"] == "competitive")
            type = ServerType.Competitive;
        else if (request.Platform == Platform.Karting)
            type = ServerType.Pod;
        
        // The game generally doesn't tell us how many slots we need, so we'll just default to 8,
        // and then update it again when we receive the event settings from the host
        int maxPlayers = 8;
        // The pod in LBPK should only be allowed to have 4 people when online
        if (type == ServerType.Pod) maxPlayers = 4;
        request.Attributes["__MAX_PLAYERS"] = maxPlayers.ToString();
        
        bool isSeries = false;
        if (request.Attributes.TryGetValue("SERIES_TYPE", out string? seriesType))
            isSeries = seriesType == "series";
        
        bool isUserOwnedGame = type is ServerType.Competitive or ServerType.Pod;
        lock (_roomLock)
        {
            int id = ++_nextGameId;

            string name = request.Attributes["SERVER_TYPE"].ToLower();
            if (request.IsRanked) name = "ranked";
            name = $"gm_{name}_{id}{(uint)TimeHelper.LocalTime}";
            var room = new GameRoom(new RoomCreationInfo
            {
                Game = new GameManagerGame
                {
                    GameName = name,
                    GameBrowserName = name,
                    GameId = id,
                    Players = new GameManagerPlayerList(),
                    Attributes = request.Attributes
                },
                Type = type,
                Platform = request.Platform,
                MaxPlayers = maxPlayers,
                OwnerUserId = isUserOwnedGame ? request.OwnerUserId : -1,
                IsRanked = request.IsRanked,
                IsSeries = isSeries
            });
        
            _rooms[name] = room;
            
            return room;
        }
    }
    
    public GameBrowserBusiestList GetBusiestCreations()
    {
        var busiest = new GameBrowserBusiestList();
        
        List<KeyValuePair<int, int>> creations;
        lock (_playerCountLock)
        {
            creations = _creationPlayerCounts.ToList();
        }
        
        creations.Sort((z, a) => a.Value.CompareTo(z.Value));
        foreach (var creation in creations)
        {
            if (busiest.Count == GameBrowserBusiestList.MaxSize) break;
            if (creation.Value == 0) continue;
            busiest.Add(creation.Key);
        }   
        
        return busiest;
    }

    public void FillCreationPlayerCounts(GamePlayerCounts counts)
    {
        lock (_playerCountLock)
        {
            foreach (int key in counts.Data.Keys.Where(key => _creationPlayerCounts.ContainsKey(key)))
            {
                counts.Data[key] = _creationPlayerCounts[key];
            }   
        }
    }

    private void IncrementPlayerCount(GameRoom room)
    {
        if (!room.Game.Attributes.TryGetValue("TRACK_CREATIONID", out string? attribute)) return;
        if (!int.TryParse(attribute, out int creationId)) return;
        lock (_playerCountLock)
        {
            if (!_creationPlayerCounts.TryAdd(creationId, 1))
                _creationPlayerCounts[creationId] += 1;
        }
    }
    
    private void DecrementPlayerCount(GameRoom room)
    {
        if (!room.Game.Attributes.TryGetValue("TRACK_CREATIONID", out string? attribute)) return;
        if (!int.TryParse(attribute, out int creationId)) return;
        lock (_playerCountLock)
        {
            if (_creationPlayerCounts.TryGetValue(creationId, out int playerCount))
                _creationPlayerCounts[creationId] = playerCount - 1;   
        }
    }

    public List<GameBrowserGame> GetKartParkSubMatches(string kartPark, Platform platform)
    {
        IEnumerable<GameRoom> rooms = GetRooms().Where(room =>
        {
            if (room.Platform != platform) return false;
            if (room.Game.Attributes.TryGetValue("KART_PARK_HOME", out string? value))
                return value == kartPark;
            return false;
        });
        
        return rooms.Select(room => room.GetGameBrowserInfo()).ToList();
    }
    
    public List<GameBrowserGame> SearchRooms(GameAttributes attributes, Platform id, int freeSlotsRequired, bool createIfNoneExist = true)
    {
        List<GameRoom> rooms = GetRooms().Where(room =>
        {
            // Don't bother checking anything if the room is full
            if (room.IsFull) return false;
            
            // Don't allow searching for other people's pod instances
            if (room.Simulation.Type == ServerType.Pod) return false;
            
            if (room.Platform != id) return false;
            if (room.NumFreeSlots < freeSlotsRequired) return false;
            
            // If the owner isn't in the game, the race either ended and the gameroom is shutting down
            // or they just haven't connected yet, so wait for that.
            if (!room.IsOwnerInGame()) return false;

            // If it's a user hosted race, wait until the host has uploaded their race settings
            // before letting people into the session.
            if (room.Simulation is { Type: ServerType.Competitive, HasRaceSettings: false })
                return false;
            
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

        return rooms.Select(room => room.GetGameBrowserInfo()).ToList();
    }
}