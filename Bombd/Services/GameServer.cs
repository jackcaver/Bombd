using System.Diagnostics.CodeAnalysis;
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

    private readonly Dictionary<string, ReservationGroup> _reservationGroups = new();
    private readonly List<PlayerJoinRequest> _playerJoinQueue = new();
    private readonly List<PlayerLeaveRequest> _playerLeaveQueue = new();
    private readonly SemaphoreSlim _playerLock = new(1);
    private readonly List<MigrationGroup> _playerMigrationGroups = new();
    public event EventHandler<PlayerJoinEventArgs>? OnPlayerJoined;
    public event EventHandler<PlayerLeaveEventArgs>? OnPlayerLeft;

    public bool ReserveSlotsInGame(string gameName, int numSlots, [MaybeNullWhen(false)] out string reservationKey)
    {
        reservationKey = null;
        _playerLock.Wait();
        try
        {
            GameRoom? room = Bombd.RoomManager.GetRoomByName(gameName);
            if (room == null) return false;
            if (room.NumFreeSlots < numSlots) return false;
            
            var slots = new Queue<int>();
            for (int i = 0; i < numSlots; ++i)
            {
                room.RequestSlot(out int slot);
                slots.Enqueue(slot);
            }

            reservationKey = CryptoHelper.GetReservationKey();
            _reservationGroups[reservationKey] = new ReservationGroup
            {
                Timestamp = TimeHelper.LocalTime,
                Slots = slots,
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

            bool isCreatingGame = request.GameName == null;
            bool isJoiningGame = !isCreatingGame;
            
            GameRoom migratedRoom;
            if (isCreatingGame)
            {
                migratedRoom = Bombd.RoomManager.CreateRoom(new CreateGameRequest
                {
                    Attributes = request.Attributes!,
                    OwnerUserId = request.HostUserId,
                    Platform = request.Platform
                });   
            } else migratedRoom = Bombd.RoomManager.GetRoomByName(request.GameName)!;
            
            var gamemanager = Bombd.GetService<GameManager>();
            int numSlotsRequired = request.PlayerIdList.Count;
            numSlotsRequired += request.Guests.Count;
            
            // If we're creating the game, we'll always have enough slots
            // So we have to make sure there are when joining
            if (isJoiningGame)
            {
                if (migratedRoom.NumFreeSlots < numSlotsRequired)
                {
                    var transaction = NetcodeTransaction.MakeRequest("gamemanager", "gameMigrationFailure");
                    transaction.Error = "notEnoughReservableSlots";
                    gamemanager.SendTransaction(request.HostUserId, transaction);
                    return;
                }   
            }
            
            var group = new MigrationGroup
            {
                OldRoom = currentRoom,
                Room = migratedRoom,
                Timestamp = TimeHelper.LocalTime,
                Owner = request.HostUserId,
                Guests = request.Guests,
                Players = new List<MigratingPlayer>()
            };
            
            foreach (GenericInt32 playerId in request.PlayerIdList)
            {
                // If the player isn't in the game, just ignore them
                if (!currentRoom.IsPlayerInGame(playerId)) continue;
                
                GamePlayer player = currentRoom.GetPlayer(playerId);
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
                gamemanager.SendTransaction(connection.UserId, transaction);
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
            
            LeaveGameInternal(player, request.Reason);
        }

        _playerLeaveQueue.Clear();
        _playerLock.Release();
    }

    private void LeaveGameInternal(GamePlayer player, string reason)
    {
        string username = player.Username;
        int userId = player.UserId;
        var room = player.Room;
        
        if (Bombd.RoomManager.TryLeaveCurrentRoom(userId))
        {
            Logger.LogInfo<GameServer>($"{username} left {room.Game.GameName}.");
            player.Room.Simulation.OnPlayerLeft(player);
            OnPlayerLeft?.Invoke(this, new PlayerLeaveEventArgs
            {
                Room = room,
                PlayerName = username,
                Reason = reason
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
                $"{username} tried to leave {room.Game.GameName}, but operation failed.");
        }
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
            if (gameRoom == null)
            {
                Logger.LogWarning<GameServer>(
                    "A player tried to join a game room, but the room doesn't exist.");
                _playerJoinQueue.RemoveAt(i--);
                continue;
            }

            // Wait until the game room is ready to join before letting the player in
            if (!gameRoom.IsReadyToJoin(request.UserId)) continue;
            
            
            // Karting doesn't use migrations and just switches games, so make sure we leave the old room.
            // TEMP: Wait until player has left?
            GamePlayer? existingPlayer = Bombd.RoomManager.GetPlayerInRoom(request.UserId);
            if (existingPlayer != null) LeaveGameInternal(existingPlayer, "gameMigration");

            if (UserInfo.TryGetValue(request.UserId, out ConnectionBase? connection))
            {
                if (!connection.IsConnected) continue;

                GamePlayer? player;
                if (request.ReservationKey != null)
                {
                    // TODO: CARRY OVER GUESTS
                    if (!_reservationGroups.TryGetValue(request.ReservationKey, out ReservationGroup group)) continue;
                    if (group.Room != gameRoom) continue;
                    int slot = group.Slots.Dequeue();
                    player = Bombd.RoomManager.JoinRoom(connection.Username, connection.UserId, slot,
                        group.Room, null);
                } 
                else player = Bombd.RoomManager.TryJoinRoom(connection.Username, connection.UserId, gameRoom, request.Guests);
                
                if (player != null)
                {
                    // For convenience, we attach the send method directly to the game player object,
                    // so that no lookups have to be performed within the simulation server environment.
                    player.Send = (bytes, type) => SendMessage(player.UserId, bytes, type);
                    player.Disconnect = () => Disconnect(player.UserId);
                    
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
                bool isOwner = player.UserId == group.Owner;
                
                // In case the player closed their game during migration or if something else caused a disconnection.
                if (!UserInfo.TryGetValue(player.UserId, out ConnectionBase? connection) || !connection.IsConnected)
                {
                    player.Status = MigrationStatus.MigrationFailed;
                    group.Room.FreeSlot(player.PlayerId);
                    continue;
                }
                
                GamePlayer gamePlayer = Bombd.RoomManager.JoinRoom(connection.Username, connection.UserId,
                    player.PlayerId, group.Room, isOwner ? group.Guests : null);

                gamePlayer.Send = (bytes, type) => SendMessage(gamePlayer.UserId, bytes, type);
                gamePlayer.Disconnect = () => Disconnect(player.UserId);
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
                gamemanager.SendTransaction(player.UserId, transaction);
            
            // If any of the old players are still connected to the gamemanager
            // tell them that they failed to migrate
            foreach (MigratingPlayer player in group.Players)
            {
                if (player.Status != MigrationStatus.MigrationFailed) continue;
                transaction = NetcodeTransaction.MakeRequest("gamemanager", "gameMigrationFailure");
                transaction.Error = "timeout";
                SendTransaction(player.UserId, transaction);
            }

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
        
        using NetworkReaderPooled reader = NetworkReaderPool.Get(data);
        if (!NetworkMessages.Unpack(reader, connection.Platform, out NetMessageType type, out int sender, out ArraySegment<byte> message))
        {
            Logger.LogError<GameServer>($"{player.Username} sent an invalid network message, disconnecting them from their current session!");
            player.Disconnect();
            return;
        }
        
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
        public int Owner;
        public GameRoom OldRoom;
        public GameRoom Room;
        public List<string> Guests;
        public List<MigratingPlayer> Players;
    }

    private struct ReservationGroup
    {
        public int Timestamp;
        public GameRoom Room;
        public Queue<int> Slots;
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