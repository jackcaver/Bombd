using Bombd.Attributes;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Serialization.Wrappers;
using Bombd.Simulation;
using Bombd.Types.Events;
using Bombd.Types.Network;
using Bombd.Types.Requests;
using Bombd.Types.Services;

namespace Bombd.Services;

[Service("gameserver", 50002, ProtocolType.RUDP)]
public class GameServer : BombdService
{
    private const int MigrationTimeout = 10000;

    private readonly Dictionary<int, ReservationGroup> _reservationGroups = new();
    private readonly List<PlayerJoinRequest> _playerJoinQueue = new();
    private readonly List<PlayerLeaveRequest> _playerLeaveQueue = new();
    private readonly SemaphoreSlim _playerLock = new(1);
    private readonly List<MigrationGroup> _playerMigrationGroups = new();
    public event EventHandler<PlayerJoinEventArgs>? OnPlayerJoined;
    public event EventHandler<PlayerLeaveEventArgs>? OnPlayerLeft;

    public bool ReserveSlotInGame(string gameName, out int reservationKey)
    {
        reservationKey = -1;
        _playerLock.Wait();
        try
        {
            GameRoom? room = Bombd.RoomManager.GetRoomByName(gameName);
            if (room == null) return false;
            if (!room.RequestSlot(out int slot)) return false;
            _reservationGroups[reservationKey] = new ReservationGroup
            {
                Timestamp = TimeHelper.LocalTime,
                Slot = slot,
                Room = room
            };

            return true;
        }
        finally
        {
            _playerLock.Release();
        }
    }

    public void AddMigrationGroup(GameMigrationRequest request)
    {
        _playerLock.Wait();
        try
        {
            // This shouldn't be normally possible, so we don't need to send back errors or anything, just return.
            GameRoom? currentRoom = Bombd.RoomManager.GetRoomByUser(request.HostUserId);
            if (currentRoom == null) return;

            GameRoom migratedRoom;
            if (request.GameName != null) migratedRoom = Bombd.RoomManager.GetRoomByName(request.GameName)!;
            else
            {
                migratedRoom = Bombd.RoomManager.CreateRoom(new CreateGameRequest
                {
                    Attributes = request.Attributes!,
                    OwnerUserId = request.HostUserId,
                    Platform = request.Platform
                });
            }

            var group = new MigrationGroup
            {
                OldRoom = currentRoom,
                Room = migratedRoom,
                Timestamp = TimeHelper.LocalTime,
                Players = new List<MigratingPlayer>()
            };

            var gamemanager = Bombd.GetService<GameManager>();
            foreach (GenericInt32 playerId in request.PlayerIdList)
            {
                GamePlayer player = currentRoom.GetPlayerByPlayerId(playerId);
                ConnectionBase connection = UserInfo[player.UserId];

                var status = MigrationStatus.WaitingForDisconnect;
                if (!migratedRoom.RequestSlot(out int slot)) status = MigrationStatus.MigrationFailed;

                group.Players.Add(new MigratingPlayer
                {
                    OldPlayerId = playerId,
                    PlayerId = slot,
                    Status = status,
                    UserId = player.UserId
                });

                if (status == MigrationStatus.MigrationFailed) continue;

                var transaction = NetcodeTransaction.MakeRequest("gamemanager", "requestDirectHostConnection");
                transaction["listenIP"] = BombdConfig.Instance.ExternalIP;
                transaction["listenPort"] = Bombd.GameServer.Port.ToString();
                transaction["hashSalt"] = CryptoHelper.Salt.ToString();
                transaction["sessionId"] = connection.SessionId.ToString();
                gamemanager.SendTransactionToUser(connection.UserId, transaction);
            }

            _playerMigrationGroups.Add(group);
        }
        finally
        {
            _playerLock.Release();
        }
    }

    public void AddPlayerToLeaveQueue(int userId, string username, string reason)
    {
        _playerLock.Wait();
        try
        {
            _playerLeaveQueue.Add(new PlayerLeaveRequest
            {
                UserId = userId,
                Username = username,
                Reason = reason
            });
        }
        finally
        {
            _playerLock.Release();
        }
    }

    public void AddPlayerToJoinQueue(PlayerJoinRequest request)
    {
        _playerLock.Wait();
        try
        {
            _playerJoinQueue.Add(request);
        }
        finally
        {
            _playerLock.Release();
        }
    }

    private void HandlePlayerLeaveRequests()
    {
        _playerLock.Wait();

        foreach (PlayerLeaveRequest request in _playerLeaveQueue)
        {
            GamePlayer? player = Bombd.RoomManager.GetPlayerInRoom(request.UserId);
            if (player == null)
            {
                Logger.LogWarning<GameServer>($"{request.Username} tried to leave a game, but they aren't in one!");
                continue;
            }

            GameRoom room = player.Room;

            if (Bombd.RoomManager.TryLeaveCurrentRoom(request.UserId))
            {
                Logger.LogInfo<GameServer>($"{request.Username} left {room.Game.GameName}.");
                player.Room.Simulation.OnPlayerLeft(player);
                OnPlayerLeft?.Invoke(this, new PlayerLeaveEventArgs
                {
                    Room = room,
                    PlayerName = request.Username,
                    Reason = request.Reason
                });
                
                if (room.IsEmpty)
                {
                    Logger.LogInfo<GameServer>($"Destroying {room.Game.GameName} since all players have left!");
                    Bombd.RoomManager.DestroyRoom(room);
                }
            }
            else
            {
                Logger.LogWarning<GameServer>(
                    $"{request.Username} tried to leave {room.Game.GameName}, but operation failed.");
            }
        }

        _playerLeaveQueue.Clear();
        _playerLock.Release();
    }

    private void HandlePlayerJoinRequests()
    {
        _playerLock.Wait();

        // TODO: If someone is in the join queue too long, discard their request
        // This usually only happens if someone closed the connection.
        for (int i = 0; i < _playerJoinQueue.Count; ++i)
        {
            PlayerJoinRequest request = _playerJoinQueue[i];

            // This generally shouldn't happen if someone isn't manually making requests,
            // but still make sure to handle these cases.
            GameRoom? gameRoom = Bombd.RoomManager.GetRoomByName(request.GameName);
            GameRoom? userRoom = Bombd.RoomManager.GetRoomByUser(request.UserId);
            if (gameRoom == null || userRoom != null)
            {
                Logger.LogWarning<GameServer>(
                    "A player tried to join a game room, but they were either already in a room, or the room doesn't exist.");
                _playerJoinQueue.RemoveAt(i--);
                continue;
            }

            if (UserInfo.TryGetValue(request.UserId, out ConnectionBase? connection))
            {
                if (!connection.IsConnected) continue;

                GamePlayer? player;
                if (request.ReservationKey != 0)
                {
                    if (!_reservationGroups.TryGetValue(request.ReservationKey, out ReservationGroup group)) continue;
                    if (group.Room != gameRoom) continue;
                    player = Bombd.RoomManager.JoinRoom(connection.Username, connection.UserId, group.Slot,
                        group.Room);
                } 
                else player = Bombd.RoomManager.TryJoinRoom(connection.Username, connection.UserId, gameRoom);
                
                if (player != null)
                {
                    // For convenience, we attach the send method directly to the game player object,
                    // so that no lookups have to be performed within the simulation server environment.
                    player.Send = (bytes, type) => SendToUser(player.UserId, bytes, type);
                    player.Disconnect = () => DisconnectUser(player.UserId);
                    
                    Logger.LogInfo<GameServer>($"{player.Username} joined {gameRoom.Game.GameName}.");

                    // Make sure to tell both the simulation instance and anything subscribed to game events
                    // about the new player that joined.
                    gameRoom.Simulation.OnPlayerJoin(player);
                    OnPlayerJoined?.Invoke(this, new PlayerJoinEventArgs
                    {
                        Room = gameRoom,
                        Player = player,
                        WasMigration = false
                    });
                }
                else
                {
                    Logger.LogWarning<GameServer>(
                        $"{connection.Username} tried to join {gameRoom.Game.GameName}, but operation failed.");
                }

                _playerJoinQueue.RemoveAt(i--);
            }
        }

        _playerLock.Release();
    }

    private void HandlePlayerMigrationRequests()
    {
        _playerLock.Wait();
        for (int i = 0; i < _playerMigrationGroups.Count; ++i)
        {
            MigrationGroup group = _playerMigrationGroups[i];
            foreach (MigratingPlayer player in group.Players)
            {
                if (player.Status >= MigrationStatus.Migrated) continue;
                if (TimeHelper.LocalTime > group.Timestamp + MigrationTimeout)
                {
                    player.Status = MigrationStatus.MigrationFailed;
                    continue;
                }

                if (!UserInfo.TryGetValue(player.UserId, out ConnectionBase? connection))
                {
                    if (player.Status == MigrationStatus.WaitingForDisconnect)
                        player.Status = MigrationStatus.WaitingForConnect;

                    continue;
                }

                if (player.Status == MigrationStatus.WaitingForConnect && connection.IsConnected)
                    player.Status = MigrationStatus.Migrated;
            }

            bool isMigrationComplete = group.Players.All(player => player.Status >= MigrationStatus.Migrated);
            if (!isMigrationComplete) continue;

            // Now that all users are connected to the gameserver, let's add them to the game room
            GameRoom room = group.Room;
            foreach (MigratingPlayer player in group.Players)
            {
                // In case the player closed their game during migration or if something else caused a disconnection.
                if (!UserInfo.TryGetValue(player.UserId, out ConnectionBase? connection) || !connection.IsConnected)
                {
                    player.Status = MigrationStatus.MigrationFailed;
                    group.Room.FreeSlot(player.PlayerId);
                    continue;
                }

                GamePlayer gamePlayer = Bombd.RoomManager.JoinRoom(connection.Username, connection.UserId,
                    player.PlayerId, group.Room);

                gamePlayer.Send = (bytes, type) => SendToUser(gamePlayer.UserId, bytes, type);
                gamePlayer.Disconnect = () => DisconnectUser(player.UserId);
                Logger.LogInfo<GameServer>($"{gamePlayer.Username} migrated to {room.Game.GameName}.");
                room.Simulation.OnPlayerJoin(gamePlayer);
                OnPlayerJoined?.Invoke(this, new PlayerJoinEventArgs
                {
                    Room = room,
                    Player = gamePlayer,
                    WasMigration = true
                });
            }

            List<GenericInt32> migratedPlayers = group.Players
                .Where(player => player.Status == MigrationStatus.Migrated)
                .Select(player => new GenericInt32(player.OldPlayerId)).ToList();

            List<GenericInt32> unmigratedPlayers = group.Players
                .Where(player => player.Status == MigrationStatus.MigrationFailed)
                .Select(player => new GenericInt32(player.OldPlayerId)).ToList();

            // Tell the old room about the migration that just occurred
            var gamemanager = Bombd.GetService<GameManager>();
            var transaction = NetcodeTransaction.MakeRequest("gamemanager", "gameMigrationOccured");
            transaction["numPlayersMigrated"] = migratedPlayers.Count.ToString();
            transaction["numPlayersNotMigrated"] = unmigratedPlayers.Count.ToString();
            transaction["playersNotMigrated"] = Convert.ToBase64String(NetworkWriter.Serialize(unmigratedPlayers));
            transaction["playersMigrated"] = Convert.ToBase64String(NetworkWriter.Serialize(migratedPlayers));
            foreach (GamePlayer player in group.OldRoom.Game.Players)
                gamemanager.SendTransactionToUser(player.UserId, transaction);


            _playerMigrationGroups.RemoveAt(i--);
        }

        _playerLock.Release();
    }

    protected override void OnGamedata(ConnectionBase connection, ArraySegment<byte> data)
    {
        // I think there should definitely be better session management storage.
        // But I'm still not entirely sure how to handle it. Seek feedback from others.
        GamePlayer? player = Bombd.RoomManager.GetPlayerInRoom(connection.UserId);
        if (player == null) return;
        
        // Network messages have a minimum of 8 bytes in their header.
        // Not going to disconnect, but we definitely shouldn't process it.
        if (data.Count < 8) return;

        // Not sure if there's any actual need for the sender, we already know who this is from the
        // connection id? Do more research, I suppose.
        using NetworkReaderPooled reader = NetworkReaderPool.Get(data);
        ArraySegment<byte> message = NetworkMessages.Unpack(reader, out NetMessageType type, out int sender);

        player.OnNetworkMessage(type, sender, message);
    }

    public override void OnTick()
    {
        List<GameRoom> rooms = Bombd.RoomManager.GetRooms();
        foreach (GameRoom room in rooms)
        {
            // Don't bother updating game sessions that are empty
            if (room.Game.Players.Count == 0) continue;
            room.Simulation.Tick();
        }

        // Handling player join/leave requests after the main server operations so there's
        // no potential issue with concurrency.
        // e.g. someone joins mid tick
        HandlePlayerMigrationRequests();
        HandlePlayerLeaveRequests();
        HandlePlayerJoinRequests();
    }

    public override void OnDisconnected(ConnectionBase connection)
    {
        Bombd.SessionManager.UnregisterSession(connection);
        UserInfo.TryRemove(connection.UserId, out _);

        if (Bombd.RoomManager.GetPlayerInRoom(connection.UserId) != null)
            AddPlayerToLeaveQueue(connection.UserId, connection.Username, "Disconnected");

        Logger.LogInfo<GameServer>($"{connection.Username} has been disconnected.");
    }

    private enum MigrationStatus
    {
        WaitingForDisconnect,
        WaitingForConnect,
        Migrated,
        MigrationFailed
    }

    private struct MigrationGroup
    {
        public int Timestamp;
        public GameRoom OldRoom;
        public GameRoom Room;
        public List<MigratingPlayer> Players;
    }

    private struct ReservationGroup
    {
        public int Timestamp;
        public GameRoom Room;
        public int Slot;
    }
    
    private class MigratingPlayer
    {
        public int OldPlayerId;
        public int PlayerId;
        public MigrationStatus Status;
        public int UserId;
    }
    
    private struct PlayerLeaveRequest
    {
        public int UserId;
        public string Username;
        public string Reason;
    }
}