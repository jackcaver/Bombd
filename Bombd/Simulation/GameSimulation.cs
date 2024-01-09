using Bombd.Configuration;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Types.Network;
using Bombd.Types.Network.Messages;
using Bombd.Types.Network.Objects;

namespace Bombd.Simulation;

public class GameSimulation
{
    public int Owner;

    private readonly List<GamePlayer> _players;
    private readonly List<PlayerInfo> _playerInfos = new();
    private readonly List<PlayerState> _playerStates = new();
    private readonly Dictionary<int, GamePlayer> _playerLookup = new();
    
    private int _seed = CryptoHelper.GetRandomSecret();
    private int _lastStateTime = TimeHelper.LocalTime;
    private readonly Dictionary<int, SyncObject> _syncObjects = new();

    public readonly GameRoom Room;
    public readonly Platform Platform;
    public readonly ServerType Type;
    
    private RaceConstants _raceConstants;
    
    private GenericSyncObject<CoiInfo> _coiInfo;
    private GenericSyncObject<VotePackage> _votePackage;
    private GenericSyncObject<GameroomState> _gameroomState;
    private GenericSyncObject<AiInfo> _aiInfo;
    private GenericSyncObject<SpectatorInfo> _raceInfo;
    private GenericSyncObject<StartingGrid> _startingGrid;
    private GenericSyncObject<EventSettings>? _raceSettings;
    private List<EventResult> _eventResults = new();

    private bool _waitingForPlayerNisEvents;
    private bool _waitingForPlayerStartEvents;
    private bool _hasSentEventResults;

    private float _pausedTimeRemaining;

    public GameSimulation(ServerType type, GameRoom room, int owner)
    {
        Room = room;
        Type = type;
        Platform = room.Platform;
        Owner = owner;
        _players = room.Game.Players;
        _raceConstants = Platform == Platform.ModNation 
            ? RaceConfig.Instance.ModNation 
            : RaceConfig.Instance.Karting;
        
        Logger.LogInfo<GameSimulation>($"Starting Game Simulation ({Type}:{Platform})");
        
        if (Type == ServerType.KartPark)
        {
            _coiInfo = CreateSystemSyncObject(WebApiManager.GetCircleOfInfluence(), NetObjectType.NetCoiInfoPackage);
        }

        if (Type == ServerType.Competitive)
        {
            if (IsKarting)
                _votePackage = CreateSystemSyncObject(new VotePackage(), NetObjectType.VotePackage);
            _gameroomState = CreateSystemSyncObject(new GameroomState(Platform), NetObjectType.GameroomState);
            _raceInfo = CreateSystemSyncObject(new SpectatorInfo(Platform), NetObjectType.SpectatorInfo);
            _aiInfo = CreateSystemSyncObject(new AiInfo(Platform), NetObjectType.AiInfo);
            _startingGrid = CreateSystemSyncObject(new StartingGrid(Platform), NetObjectType.StartingGrid);
        }
    }

    private void AddFakePlayer(string username)
    {
        string openuid = CryptoHelper.GetRandomSecret().ToString("x") + username;
        int nameUid = CryptoHelper.StringHash32(openuid);
        
        int userId = CryptoHelper.GetRandomSecret();
        int playerId = CryptoHelper.GetRandomSecret();

        var player = new GamePlayer(Room, username, userId, playerId)
        {
            IsFakePlayer = true
        };
        
        player.State.Update(new PlayerState
        {
            PlayerConnectId = userId,
            NameUid = (uint)nameUid,
            KartId = 0x1324,
            CharacterId = 0x132c,
            KartSpeedAccel = 0x0
        });

        player.Info = new PlayerInfo
        {
            Operation = GameJoinStatus.RacerPending,
            NetcodeUserId = userId,
            NetcodeGamePlayerId = playerId,
            PlayerConnectId = userId,
            GuestOfPlayerNameUid = CryptoHelper.GetRandomSecret(),
            IsGroupLeader = false,
            PlayerGroupId = CryptoHelper.GetRandomSecret(),
            NameUid = openuid,
            PlayerName = username,
            PodLocation = string.Empty
        };
        
        _players.Add(player);
        _playerLookup[player.UserId] = player;
        _playerInfos.Add(player.Info);
        _playerStates.Add(player.State);

        byte[] charDataBlob;
        byte[] kartDataBlob;
        if (Platform == Platform.Karting)
        {
            charDataBlob = File.ReadAllBytes("Data/Blobs/Karting/CharDataBlob");
            kartDataBlob = File.ReadAllBytes("Data/Blobs/Karting/KartDataBlob");
        }
        else
        {
            charDataBlob = File.ReadAllBytes("Data/Blobs/ModNation/CharDataBlob");
            kartDataBlob = File.ReadAllBytes("Data/Blobs/ModNation/KartDataBlob");
        }
        
        CreateGenericSyncObject(new PlayerConfig(Platform)
        {
            Type = 0,
            NetcodeUserId = userId,
            CharCreationId = 0x132c,
            KartCreationId = 0x1324,
            UidName = openuid,
            Username = username,
            CharDataBlob = charDataBlob,
            KartDataBlob = kartDataBlob
        }, NetObjectType.PlayerConfig, username, userId);

        if (Platform == Platform.Karting)
        {
            CreateGenericSyncObject(new PlayerAvatar
            {
                OwnerName = username,
                SmileyDataBlob = File.ReadAllBytes("Data/Blobs/Karting/SmileyDataBlob"),
                FrownyDataBlob = File.ReadAllBytes("Data/Blobs/Karting/FrownyDataBlob")
            }, NetObjectType.PlayerAvatar, username, userId);   
        }
    }

    public bool IsKarting => Platform == Platform.Karting;
    public bool IsModNation => Platform == Platform.ModNation;
    public bool HasRaceSettings => _raceSettings != null;

    public bool IsHostReady()
    {
        if (Owner == -1) return true;
        if (!_playerLookup.TryGetValue(Owner, out GamePlayer? player)) return false;
        
        var state = player.State;
        if (Type != ServerType.Competitive) return !state.IsConnecting;
        return !state.IsConnecting && (state.Flags & PlayerStateFlags.GameRoomReady) != 0;
    }

    private void SwitchAllToRacers()
    {
        foreach (var player in _players)
            player.IsSpectator = false;
        foreach (var info in _playerInfos)
            info.Operation = GameJoinStatus.RacerPending;
        
        if (IsKarting)
            BroadcastKartingPlayerSessionInfo();
        else
            BroadcastMessage(new NetMessagePlayerSessionInfo { JoinStatus = GameSessionStatus.SwitchAllToRacer }, PacketType.ReliableGameData);
    }

    private void BroadcastKartingPlayerSessionInfo()
    {
        if (!IsKarting) return;
        BroadcastMessage(new NetMessagePlayerCreateInfo { Data = _playerInfos }, PacketType.ReliableGameData);
    }
    
    public void OnPlayerJoin(GamePlayer player)
    {
        // Don't actually know if this is random per room or random per player,
        // I assume it's used for determinism, but I don't know?
        player.SendReliableMessage(new NetMessageRandomSeed { Seed = _seed });
        
        // Add player to lookup cache
        _playerLookup[player.UserId] = player;
        _playerStates.Add(player.State);
        
        if (IsModNation)
        {
            player.IsSpectator = !CanJoinAsRacer();
            
            // Tell everybody else about yourself
            foreach (GamePlayer peer in _players)
            {
                GameSessionStatus status =
                    player.IsSpectator ? GameSessionStatus.JoinAsSpectator : GameSessionStatus.JoinAsRacer;
                
                peer.SendReliableMessage(new NetMessagePlayerSessionInfo
                {
                    JoinStatus = status,
                    PlayerId = player.PlayerId,
                    UserId = player.UserId
                });
            }

            // Tell the player about everyone who is in the game.
            foreach (GamePlayer peer in _players)
            {
                if (player == peer) continue;
                GameSessionStatus status =
                    peer.IsSpectator ? GameSessionStatus.JoinAsSpectator : GameSessionStatus.JoinAsRacer;
                player.SendReliableMessage(new NetMessagePlayerSessionInfo
                {
                    JoinStatus = status,
                    PlayerId = peer.PlayerId,
                    UserId = peer.UserId
                });
            }
        } else BroadcastKartingPlayerSessionInfo();

        // Initialize any sync objects that exist
        foreach (SyncObject syncObject in _syncObjects.Values)
        {
            var message = MakeCreateSyncObjectMessage(syncObject);
            
            // Needs to be sent twice with a create and update message,
            // they use the same net message type in Modnation despite having
            // separate network event types, but whatever.
            player.SendReliableMessage(message);

            // If the segment is empty, it probably hasn't been initialized
            // by the owner yet, don't send it.
            if (syncObject.Data.Count != 0)
            {
                message = MakeUpdateSyncObjectMessage(syncObject);
                player.SendReliableMessage(message);
            }
        }
    }
    
    public void OnPlayerLeft(GamePlayer player)
    {
        // Destroy cached states
        _playerStates.Remove(player.State);
        _playerLookup.Remove(player.UserId);
        _playerInfos.RemoveAll(info => info.NetcodeUserId == player.UserId);
        
        // Destroy all sync objects owned by the player, this will realistically only be the player info object,
        // but just to be safe search for all owned objects anyway.
        IEnumerable<KeyValuePair<int, SyncObject>>
            owned = _syncObjects.Where(x => x.Value.OwnerUserId == player.UserId);
        foreach (KeyValuePair<int, SyncObject> pair in owned)
        {
            var message = MakeRemoveSyncObjectMessage(pair.Value);
            BroadcastMessage(player, message, PacketType.ReliableGameData);
            _syncObjects.Remove(pair.Key);
        }

        // If the player hasn't sent a leave reason, send a generic one
        if (!player.HasSentLeaveReason)
        {
            BroadcastMessage(new NetMessagePlayerLeave(Platform)
            {
                PlayerName = player.Username,
                Reason = 0
            }, PacketType.ReliableGameData);
        }
        
        if (_players.Count == 0) return;
        
        // Make sure to re-order the pod if necessary
        if (Type == ServerType.Pod) RecalculatePodPositions();
        
        // If we're in Karting and in the voting stage, make sure to remove our vote
        if (IsKarting && Type == ServerType.Competitive && _votePackage.Value.IsVoting)
        {
            _votePackage.Value.RemoveVote(player.UserId);
        }
        
        // If we're the owner, change the host to someone random
        if (player.UserId == Owner)
        {
            var random = new Random();
            int index = random.Next(0, _players.Count);
            var randomPlayer = _players[index];

            Owner = randomPlayer.UserId;
            if (_raceSettings != null)
            {
                _raceSettings.Value.OwnerNetcodeUserId = Owner;
                _raceSettings.Sync();
            }
        }
    }

    private void RecalculatePodPositions()
    {
        if (!IsKarting) return;
        
        int position = 1;
        foreach (PlayerInfo info in _playerInfos)
           info.PodLocation = $"POD_Player0{position++}_Placer";
        
        // Send the new session info to everybody in the game
        BroadcastKartingPlayerSessionInfo();
    }

    private void BroadcastVoipData(GamePlayer sender, ArraySegment<byte> data)
    {
        foreach (var player in _players)
        {
            if (player == sender) continue;
            player.Send(data, PacketType.VoipData);
        }
    }
    
    private void BroadcastGenericIntMessage(int value, NetMessageType messageType, PacketType packetType)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> message = NetworkMessages.PackInt(writer, value, messageType);
        foreach (GamePlayer player in _players)
        {
            player.Send(message, packetType);
        }
    }
    
    private void BroadcastGenericMessage(GamePlayer sender, ArraySegment<byte> data, NetMessageType messageType, PacketType packetType)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> message = NetworkMessages.PackData(writer, data, messageType);
        foreach (GamePlayer player in _players)
        {
            if (player == sender) continue;
            player.Send(message, packetType);
        }
    }
    
    private void BroadcastGenericMessage(ArraySegment<byte> data, NetMessageType messageType, PacketType packetType)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> message = NetworkMessages.PackData(writer, data, messageType);
        foreach (GamePlayer player in _players)
        {
            player.Send(message, packetType);
        }
    }

    private void BroadcastMessage(GamePlayer sender, INetworkMessage message, PacketType type)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> payload = NetworkMessages.Pack(writer, message);
        foreach (GamePlayer player in _players)
        {
            if (player == sender) continue;
            player.Send(payload, type);
        }
    }
    
    private void BroadcastMessage(INetworkMessage message, PacketType type)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> payload = NetworkMessages.Pack(writer, message);
        foreach (GamePlayer player in _players) player.Send(payload, type);
    }
    
    private void BroadcastPlayerStates()
    {
        if (IsKarting)
        {
            BroadcastMessage(new NetMessageKartingPlayerUpdate { StateUpdates = _playerStates }, PacketType.ReliableGameData);
            return;
        }
        
        BroadcastMessage(new NetMessageModnationPlayerUpdate { StateUpdates = _playerStates }, PacketType.ReliableGameData);
    }

    private void UpdateVotePackage()
    {
        if (_raceSettings == null) return;
        
        _votePackage.Value.StartVote(_raceSettings.Value.CreationId);
        _votePackage.Sync();
    }
    
    private void SetCurrentGameroomState(RoomState state)
    {
        var room = _gameroomState.Value;
        var oldState = room.State;
        if (state == oldState) return;
        
        Logger.LogInfo<GameSimulation>($"Setting GameRoomState to {state}");

        // Make sure we cache how much time is remaining when we paused the countdown
        if (state == RoomState.CountingDownPaused)
        {
            _pausedTimeRemaining = (room.LoadEventTime - TimeHelper.LocalTime);
        }
        
        room.State = state;
        room.LoadEventTime = 0;
        room.LockedTimerValue = 0.0f;
        room.LockedForRacerJoinsValue = 0.0f;
        
        if (state <= RoomState.Ready || state == RoomState.RaceInProgress)
        {
            // Reset the race loading flags for each player
            foreach (var player in _players)
            {
                player.State.Flags &= ~PlayerStateFlags.RaceLoadFlags;
                player.HasSentRaceResults = false;
            }
        }
        
        switch (state)
        {
            case RoomState.None:
            { 
                _waitingForPlayerNisEvents = false; 
                _waitingForPlayerStartEvents = false; 
                _hasSentEventResults = false;
                foreach (var player in _players)
                {
                    // We only need to reset the flags for people who are currently racing
                    if (player.IsSpectator) continue;
                    player.State.Flags = PlayerStateFlags.None;
                    
                    // Set the state back to connecting so people in lobby can see whether you're
                    // back or not yet
                    player.State.IsConnecting = true;
                    player.State.WaitingForPlayerConfig = true;
                }
                BroadcastPlayerStates();
                SwitchAllToRacers();
                break;
            }
            case RoomState.RaceInProgress:
            {
                _waitingForPlayerNisEvents = true;
                break;
            }
            case RoomState.Ready:
            {
                // If the gameroom isn't loaded before it receives the player create info,
                // it won't ever update the racer state
                SwitchAllToRacers();
                break;
            }
            case RoomState.CountingDown:
            {
                UpdateRaceSetup();
                
                room.LoadEventTime = TimeHelper.LocalTime + _raceConstants.GameRoomCountdownTime;
                room.LockedForRacerJoinsValue = _raceConstants.GameRoomTimerRacerLock;
                room.LockedTimerValue = _raceConstants.GameRoomTimerLock;   

                // Restore the old timer state pre-pause
                if (oldState == RoomState.CountingDownPaused)
                {
                    room.LoadEventTime = TimeHelper.LocalTime + (int)_pausedTimeRemaining;
                }
                
                break;
            }
        }

        _lastStateTime = TimeHelper.LocalTime;
        _gameroomState.Sync();
    }
    
    private GenericSyncObject<T> CreateSystemSyncObject<T>(T instance, NetObjectTypeInfo typeInfo) where T : INetworkWritable
    {
        return CreateGenericSyncObject(instance, typeInfo, NetworkMessages.SimServerName, -1);
    }
    
    private GenericSyncObject<T> CreateGenericSyncObject<T>(T instance, NetObjectTypeInfo typeInfo, string owner, int userId) where T : INetworkWritable
    {
        int type = typeInfo.ModnationTypeId;
        if (IsKarting)
            type = typeInfo.KartingTypeId;
        
        var syncObject = new GenericSyncObject<T>(instance, type, owner, userId);
        _syncObjects[syncObject.Guid] = syncObject;
        syncObject.OnUpdate = () =>
        {
            var message = MakeUpdateSyncObjectMessage(syncObject);
            BroadcastMessage(message, PacketType.ReliableGameData);
        };
        
        if (_players.Count != 0)
        {
            BroadcastMessage(MakeCreateSyncObjectMessage(syncObject), PacketType.ReliableGameData);
            BroadcastMessage(MakeUpdateSyncObjectMessage(syncObject), PacketType.ReliableGameData);
        }
        
        Logger.LogInfo<GameSimulation>($"{syncObject} has been created by the system.");
        
        return syncObject;
    }
    
    private void UpdateRaceSetup()
    {
        if (_raceSettings == null) return;
        
        _startingGrid.Value.Clear();
        foreach (GamePlayer player in _players)
        {
            if (!player.State.HasNameUid) continue;
            _startingGrid.Value.Add(new GridPositionData((int)player.State.NameUid, false));
            if (player.Guest != null)
                _startingGrid.Value.Add(new GridPositionData(player.Guest.NameUid, true));
        }
        
        int maxAi = _aiInfo.Value.DataSet.Length;
        int maxHumans = _raceSettings.Value.MaxHumans;
        int numAi = Math.Min(maxAi, maxHumans - _startingGrid.Value.Count);
        if (numAi <= 0) numAi = 0;
        
        // AI are owned by players, if one player disconnects, their AI is destroyed,
        // so distribute them between all players
        List<string> playerNames = _players.Select(player => player.Username).ToList();
        _aiInfo.Value = _raceSettings.Value.AiEnabled ? 
            new AiInfo(Platform, playerNames, numAi) : new AiInfo(Platform);

        for (int i = 0; i < _aiInfo.Value.Count; ++i)
        {
            _startingGrid.Value.Add(new GridPositionData(
                CryptoHelper.StringHash32(_aiInfo.Value.DataSet[i].UidName),
                false
            ));
        }   
        
        if (IsKarting)
        {
            _raceSettings.Value.NumHoard = numAi;
            _raceSettings.Sync();   
        }

        _startingGrid.Sync();
    }
    
    private bool CanJoinAsRacer()
    {
        if (Type != ServerType.Competitive) return true;
        var room = _gameroomState.Value;
        
        if (room.State == RoomState.CountingDown)
        {
            int pointOfNoReturn = room.LoadEventTime - (int)room.LockedForRacerJoinsValue;
            if (TimeHelper.LocalTime >= pointOfNoReturn)
                return false;
        }
        
        return room.State != RoomState.RaceInProgress;
    }

    private SyncObject? GetOwnedSyncObject(int guid, GamePlayer player)
    {
        if (!_syncObjects.TryGetValue(guid, out SyncObject? syncObject))
        {
            Logger.LogWarning<GameSimulation>($"{player.Username} tried to perform an operation on a SyncObject that doesn't exist! ({guid})");
            return null;
        }
        
        if (syncObject.OwnerUserId != player.UserId)
        {
            Logger.LogWarning<GameSimulation>($"{player.Username} tried to perform an operation on a SyncObject that they don't own!");
            return null;
        }

        return syncObject;
    }
    
    private INetworkMessage MakeCreateSyncObjectMessage(SyncObject syncObject)
    {
        if (IsKarting) 
            return new NetMessageSyncObjectCreate(syncObject);
        
        return new NetMessageSyncObject(syncObject, NetObjectMessageType.Create);
    }

    private INetworkMessage MakeUpdateSyncObjectMessage(SyncObject syncObject)
    {
        if (IsKarting) 
            return new NetMessageSyncObjectUpdate(syncObject);
        
        return new NetMessageSyncObject(syncObject, NetObjectMessageType.Update);
    }

    private INetworkMessage MakeRemoveSyncObjectMessage(SyncObject syncObject)
    {
        if (IsKarting) 
            return new NetMessageSyncObjectRemove(syncObject);
        
        return new NetMessageSyncObject(syncObject, NetObjectMessageType.Remove);
    }
    
    private bool PersistUserSyncedObject(SyncObject syncObject, GamePlayer player)
    {
        if (_syncObjects.ContainsKey(syncObject.Guid))
        {
            Logger.LogWarning<GameSimulation>($"{player.Username} tried to create a SyncObject with a GUID that already exists!");
            return false;
        }
        
        Logger.LogInfo<GameSimulation>(
            $"Creating SyncObject({syncObject}) with type {syncObject.Type} and owner {syncObject.OwnerName}");
        _syncObjects[syncObject.Guid] = syncObject;
        return true;
    }

    private void ParsePlayerConfig(ArraySegment<byte> data, GamePlayer player)
    {
        try
        {
            var config = PlayerConfig.ReadVersioned(data, Platform);

            // Type 1 is guest, we need to extract the name uid on ModNation
            if (config.Type == 1)
            {
                GameGuest? guest = player.GetGuestByName(config.Username);
                if (guest == null || (IsKarting && config.NetcodeUserId != -1) ||
                    (IsModNation && config.NetcodeUserId != player.UserId))
                {
                    Logger.LogWarning<GameSimulation>(
                        $"Guest doesn't belong to {player.Username}, this shouldn't happen, disconnecting!");
                    player.Disconnect();
                    return;
                }

                // On Karting, we already got the NameUid from the PlayerInfoCreate message
                if (IsModNation)
                    guest.NameUid = CryptoHelper.StringHash32(config.UidName);
            }
            else if (config.Type == 0)
            {
                if (config.NetcodeUserId != player.UserId)
                {
                    Logger.LogWarning<GameSimulation>(
                        $"PlayerConfig doesn't belong to {player.Username}, this shouldn't happen, disconnecting!");
                    player.Disconnect();
                    return;
                }

                player.State.WaitingForPlayerConfig = false;
            }
            else
            {
                Logger.LogWarning<GameSimulation>(
                    $"PlayerConfig for {player.Username} has invalid type! Disconnecting!");
                player.Disconnect();
            }
        }
        catch (Exception)
        {
            Logger.LogError<GameSimulation>(
                $"Failed to parse PlayerConfig for {player.Username}'s guest, disconnecting them from the game session.");
            player.Disconnect();
        }
    }
    
    private bool UpdateUserSyncObject(int guid, ArraySegment<byte> data, GamePlayer player)
    {
        SyncObject? syncObject = GetOwnedSyncObject(guid, player);
        if (syncObject == null) return false;
        
        Logger.LogInfo<GameSimulation>($"Updating SyncObject({syncObject})");
        
        byte[] array = new byte[data.Count];
        data.CopyTo(array);
        syncObject.Data = new ArraySegment<byte>(array);
        

        // Check if we've received the player config object yet
        var playerConfigType = NetObjectType.PlayerConfig;
        bool isKartingConfig = Platform == Platform.Karting && syncObject.Type == playerConfigType.KartingTypeId;
        bool isModNationConfig = Platform == Platform.ModNation && syncObject.Type == playerConfigType.ModnationTypeId;
        if (isModNationConfig || isKartingConfig)
            ParsePlayerConfig(data, player);
        
        return true;
    }

    private bool RemoveUserSyncObject(int guid, GamePlayer player)
    {
        SyncObject? syncObject = GetOwnedSyncObject(guid, player);
        if (syncObject == null) return false;
        
        Logger.LogInfo<GameSimulation>($"Removing SyncObject({syncObject})");
        _syncObjects.Remove(guid);
        return true;
    }
    
    public void OnNetworkMessage(GamePlayer player, int senderNameUid, NetMessageType type, ArraySegment<byte> data)
    {
        if (
            type != NetMessageType.VoipPacket && 
            type != NetMessageType.MessageUnreliableBlock && 
            type != NetMessageType.MessageReliableBlock &&
            type != NetMessageType.GenericGameplay &&
            type != NetMessageType.Gameplay)
        {
            Logger.LogTrace<GameSimulation>($"Received NetMessage {type} from {player.Username} ({(uint)player.UserId}:{(uint)player.PlayerId})");   
        }
        
        switch (type)
        {
            case NetMessageType.PlayerDetachGuestInfo:
            {
                NetMessagePlayerCreateInfo message;
                try
                {
                    message = NetworkReader.Deserialize<NetMessagePlayerCreateInfo>(data);
                }
                catch (Exception)
                {
                    Logger.LogWarning<GameSimulation>($"Failed to parse PlayerDetachGuestInfo for {player.Username}, disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }

                foreach (var detachInfo in message.Data)
                {
                    int index = _playerInfos.FindIndex(info =>
                        info.PlayerName == detachInfo.PlayerName && info.NetcodeUserId == player.UserId);
                    if (index != -1)
                        _playerInfos.RemoveAt(index);
                }
                
                // Tell everybody else that we've detached the guest(s)
                BroadcastGenericMessage(data, type, PacketType.ReliableGameData);
                
                // Recalculate the pod positions
                if (Type == ServerType.Pod) RecalculatePodPositions();
                
                break;
            }
            case NetMessageType.ItemMessage_0x10:
            case NetMessageType.ItemDestroy:
            case NetMessageType.ItemHitPlayer:
            case NetMessageType.ItemHitConfirm:
            case NetMessageType.SecondaryCollision:
            {
                // TODO: Item Validation 
                //  - Verify that the player actually has the item before
                //  - either destroying it or accepting a hit player message.
                
                // ItemHitConfirm gets sent by the player that's being hit, so
                // we don't have to verify that message.
                
                BroadcastGenericMessage(data, type, PacketType.ReliableGameData);
                break;
            }
            case NetMessageType.ArbitratedItemCreateBlock:
            case NetMessageType.ArbitratedItemDestroyBlock:
            {
                // I assume this has to be sent to everyone else on the server
                // We might also have to actually keep track of the item ids?
                
                BroadcastGenericMessage(data, type, PacketType.ReliableGameData);
                break;
            }
            case NetMessageType.ArbitratedItemAcquire:
            case NetMessageType.ArbitratedItemRelease:
            {
                // This message can be responded to with a failure, what's the case for that?
                // Is it just if it's currently in timeout?
                    // E.g., you grabbed the item, but you didn't send the acquire response fast enough
                    // so somebody else acquired it and the item is currently in a timeout phase, so you cant
                    // acquire it?
                BroadcastGenericMessage(data, type, PacketType.ReliableGameData);
                break;
            }
            case NetMessageType.PlayerCreateInfo:
            {
                NetMessagePlayerCreateInfo message;
                try
                {
                    message = NetworkReader.Deserialize<NetMessagePlayerCreateInfo>(data);
                }
                catch (Exception)
                {
                    Logger.LogWarning<GameSimulation>($"Failed to parse PlayerCreateInfo for {player.Username}, disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }

                if (message.Data.Count != player.Guests.Count + 1)
                {
                    Logger.LogWarning<GameSimulation>($"PlayerCreateInfo from {player.Username} doesn't contain correct number of infos! Disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }
                
                var info = message.Data[0];

                if (info.NetcodeUserId != player.UserId || info.NetcodeGamePlayerId != player.PlayerId)
                {
                    Logger.LogWarning<GameSimulation>($"PlayerCreateInfo from {player.Username} doesn't match their connection details! Disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }
                
                // Player create info gets sent again when attaching guests, make sure we remove all our old infos
                _playerInfos.RemoveAll(x => x.NetcodeUserId == player.UserId);
                
                player.IsSpectator = !CanJoinAsRacer();
                GameJoinStatus status =
                    player.IsSpectator ? GameJoinStatus.SpectatorPending : GameJoinStatus.RacerPending;
                
                // The player create info gets sent to the server with an operation of type none,
                // send it to the other players telling them we're joining.
                info.Operation = status;
                
                // Make sure to track our player info
                player.Info = info;
                _playerInfos.Add(info);
                
                // Handle additional guest player infos attached
                for (int i = 1; i < message.Data.Count; ++i)
                {
                    var guestInfo = message.Data[i];
                    var guest = player.GetGuestByName(guestInfo.PlayerName);
                    if (guest == null || guestInfo.NetcodeUserId != player.UserId || guestInfo.NetcodeGamePlayerId != player.PlayerId)
                    {
                        Logger.LogWarning<GameSimulation>($"PlayerCreateInfo from {player.Username} contains invalid guest info! Disconnecting them from the session.");
                        player.Disconnect();
                        break;
                    }
                    
                    // Cache the name uid for the starting grid
                    guest.NameUid = CryptoHelper.StringHash32(guestInfo.NameUid);
                    
                    guestInfo.Operation = status;
                    guest.Info = guestInfo;
                    _playerInfos.Add(guestInfo);
                }
                
                // Does the player need their status back? If we changed the status maybe, but we should probably
                // just send it back when their gameroom is ready.
                BroadcastMessage(message, PacketType.ReliableGameData);
                
                
                // Since we have the player info now, we can recalculate the positions in
                // pod, if relevant
                if (Type == ServerType.Pod) 
                    RecalculatePodPositions();
                
                break;
            }
            case NetMessageType.WorldobjectCreate:
            {
                BroadcastGenericMessage(data, NetMessageType.WorldobjectCreate, PacketType.ReliableGameData);
                break;
            }
            case NetMessageType.LeaderChangeRequest:
            {
                // Only the current session leader can change the host
                if (player.UserId != Owner) break;
                // Make sure we're in a valid gameroom state
                if (Type != ServerType.Competitive || _raceSettings == null) break;
                
                // Data must be 4 bytes since it's just the new host's nameuid
                if (data.Count != 4)
                {
                    Logger.LogWarning<GameSimulation>($"Failed to parse LeaderChangeRequest from {player.Username}, disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }
                
                uint nameUid = 0;
                nameUid |= (uint)(data[0] << 24);
                nameUid |= (uint)(data[1] << 16);
                nameUid |= (uint)(data[2] << 8);
                nameUid |= (uint)(data[3] << 0);
                
                // Find the player by nameUID, if they exist, set them to host
                foreach (var racer in _players)
                {
                    if (racer.State.NameUid != nameUid) continue;
                    _raceSettings.Value.OwnerNetcodeUserId = racer.UserId;
                    _raceSettings.Sync();
                    Owner = racer.UserId;
                    break;
                }
                
                break;
            }
            case NetMessageType.PlayerLeave:
            {
                NetMessagePlayerLeave message;
                try
                {
                    message = NetMessagePlayerLeave.ReadVersioned(data, Platform);
                }
                catch (Exception)
                {
                    Logger.LogWarning<GameSimulation>(
                        $"Failed to parse PlayerLeave message from {player.Username}, disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }
                
                // The message doesn't include the player's username for whatever reason, just their leave reason.
                message.PlayerName = player.Username;
                player.HasSentLeaveReason = true;
                
                BroadcastMessage(player, message, PacketType.ReliableGameData);
                break;
            }
            case NetMessageType.GameroomReady:
            {
                player.State.Flags |= PlayerStateFlags.GameRoomReady;
                break;
            }
            case NetMessageType.GameroomStopTimer:
            {
                if (player.UserId != Owner) break;
                
                // Make sure we're still within the threshold of being able to stop the timer
                var room = _gameroomState.Value;
                float timeRemaining = (room.LoadEventTime - TimeHelper.LocalTime);
                if (timeRemaining > _raceConstants.GameRoomTimerRacerLock)
                {
                    SetCurrentGameroomState(RoomState.Ready);
                }
                
                break;
            }
            case NetMessageType.PostRaceVoteForTrack:
            {
                if (IsKarting)
                {
                    var votes = _votePackage.Value;
                    
                    // If we're not in a voting state, don't accept any votes
                    if (!votes.IsVoting) break;
                    
                    // Data must be 4 bytes since it's just the voted track id
                    if (data.Count != 4)
                    {
                        Logger.LogWarning<GameSimulation>($"Failed to parse PostRaceVoteForTrack from {player.Username}, disconnecting them from the session.");
                        player.Disconnect();
                        break;
                    }
                    
                    int trackId = 0;
                    trackId |= data[0] << 24;
                    trackId |= data[1] << 16;
                    trackId |= data[2] << 8;
                    trackId |= data[3] << 0;
                    
                    votes.CastVote(player.UserId, trackId);
                    _votePackage.Sync();
                }
                
                break;
            }
            case NetMessageType.SpectatorInfo:
            {
                // Only the owner should be able to update the spectator info for the gameroom
                if (player.UserId != Owner) break;
                
                try
                {

                    RaceState oldState = _raceInfo.Value.RaceState;
                    _raceInfo.Value = SpectatorInfo.ReadVersioned(data, Platform);
                    RaceState newState = _raceInfo.Value.RaceState;

                    if (oldState == RaceState.PostRace && newState == RaceState.Invalid)
                    {
                        SetCurrentGameroomState(RoomState.None);
                        if (IsKarting && _votePackage.Value.IsVoting)
                        {
                            var votes = _votePackage.Value;
                            if (votes.NumPlayersVoted != 0)
                            {
                                votes.FinishVote();
                                _votePackage.Sync();
                            }
                            
                            _votePackage.Value.Reset();
                            _votePackage.Sync();
                        }
                    }
                    
                    Logger.LogDebug<GameSimulation>("RaceState: " + _raceInfo.Value.RaceState);
                    Logger.LogDebug<GameSimulation>("RaceEndServerTime: " + _raceInfo.Value.RaceEndServerTime);
                    Logger.LogDebug<GameSimulation>("PostRaceServerTime: " + _raceInfo.Value.PostRaceServerTime);
                    
                }
                catch (Exception)
                {
                    Logger.LogWarning<GameSimulation>($"Failed to parse SpectatorInfo from {player.Username}, disconnecting them from the session.");
                    player.Disconnect();
                }
                
                break;
            }
            case NetMessageType.GameroomDownloadTracksComplete:
            {
                player.State.Flags |= PlayerStateFlags.DownloadedTracks;
                break;
            }
            case NetMessageType.ReadyForEventStart:
            {
                player.State.Flags |= PlayerStateFlags.ReadyForEvent;
                break;
            }
            case NetMessageType.ReadyForNisStart:
            {
                player.State.Flags |= PlayerStateFlags.ReadyForNis;
                break;
            }
            case NetMessageType.GameroomRequestStartEvent:
            {
                // Only the host should be allowed to start the event I'm fairly sure
                if (player.UserId != Owner) break;
                SetCurrentGameroomState(RoomState.DownloadingTracks);
                break;
            }
            case NetMessageType.EventResultsPreliminary:
            {
                List<EventResult> results;
                try
                {
                    using NetworkReaderPooled reader = NetworkReaderPool.Get(data);

                    int len = reader.Capacity;
                    if (IsKarting)
                    {
                        // Don't really care about this data
                        reader.Offset += 0xc;
                        len = reader.ReadInt32();
                    }
                    
                    results = EventResult.Deserialize(reader.ReadString(len));
                }
                catch (Exception)
                {
                    Logger.LogWarning<GameSimulation>($"Failed to parse EventResultsPreliminary for {player.Username}, disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }

                player.HasSentRaceResults = true;
                _eventResults.AddRange(results);
                break;
            }
            case NetMessageType.GroupLeaderMatchmakingStatus:
            case NetMessageType.GenericGameplay:
            case NetMessageType.PlayerFinishedEvent:
            case NetMessageType.Gameplay:
            case NetMessageType.WandererUpdate:
            case NetMessageType.TextChatMsg:
            case NetMessageType.InviteChallengeMessageModnation:
            case NetMessageType.InviteSessionJoinDataModnation:
            case NetMessageType.InviteRequestJoin:
            {
                BroadcastGenericMessage(player, data, type, PacketType.ReliableGameData);
                break;
            }
            case NetMessageType.EventSettingsUpdate:
            {
                // Only the host should be allowed to update event settings I'm fairly sure
                if (player.UserId != Owner) break;

                EventSettings settings;
                try
                {
                    settings = EventSettings.ReadVersioned(data, Platform);
                }
                catch (Exception)
                {
                    Logger.LogWarning<GameSimulation>($"Failed to parse EventSettingsUpdate from {player.Username}, disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }

                // Can't limit to less people than we actually have
                if (settings.MaxHumans < _players.Count)
                    break;

                // Since we got new settings, we should update the room attributes for matchmaking
                Room.UpdateAttributes(settings);
                
                if (_raceSettings != null) _raceSettings.Value = settings;
                else _raceSettings = CreateSystemSyncObject(settings, NetObjectType.RaceSettings);
                
                break;
            }
            case NetMessageType.VoipPacket:
            {
                BroadcastVoipData(player, data);
                break;
            }
            case NetMessageType.MessageReliableBlock:
            {
                BroadcastGenericMessage(player, data, NetMessageType.MessageReliableBlock, PacketType.ReliableGameData);
                break;
            }
            case NetMessageType.MessageUnreliableBlock:
            {
                // The only unreliable message block I've seen get sent is just input data,
                // and it just gets broadcasted to all other players, should probably check for other conditions?
                // But it seems fine for now.
                BroadcastGenericMessage(player, data, NetMessageType.MessageUnreliableBlock, PacketType.UnreliableGameData);
                break;
            }
            
            case NetMessageType.PlayerStateUpdate:
            {
                PlayerState state;
                try
                {
                    if (IsKarting)
                    {
                        var message = NetworkReader.Deserialize<NetMessageKartingPlayerUpdate>(data);
                        state = message.StateUpdates.ElementAt(0);
                    }
                    else
                    {
                        state = PlayerState.LoadXml(data);
                    }
                }
                catch (Exception)
                {
                    Logger.LogWarning<GameSimulation>($"Invalid PlayerStateUpdate received from {player.Username}, disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }

                // Patch our existing player state with the new message
                player.State.Update(state);
                
                // Wait until we've received the player config and the second player state update
                // to finish our "connecting" process.
                if (player.State is { IsConnecting: true, WaitingForPlayerConfig: false })
                {
                    player.State.IsConnecting = false;
                    BroadcastKartingPlayerSessionInfo();
                    if (Type == ServerType.Competitive && _gameroomState.Value.State == RoomState.CountingDown)
                        UpdateRaceSetup();
                }
                
                BroadcastPlayerStates();
                
                break;
            }

            case NetMessageType.SyncObjectRemove:
            {
                NetMessageSyncObjectRemove message;
                try
                {
                    message = NetworkReader.Deserialize<NetMessageSyncObjectRemove>(data);
                }
                catch (Exception)
                {
                    Logger.LogWarning<GameSimulation>($"Failed to parse SyncObjectRemove message from {player.Username}, disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }
                
                if (RemoveUserSyncObject(message.Guid, player))
                {
                    BroadcastGenericMessage(player, data, type, PacketType.ReliableGameData);
                }
                break;
            }
            
            case NetMessageType.SyncObjectUpdate:
            {
                NetMessageSyncObjectUpdate message;
                try
                {
                    message = NetworkReader.Deserialize<NetMessageSyncObjectUpdate>(data);
                }
                catch (Exception)
                {
                    Logger.LogWarning<GameSimulation>($"Failed to parse SyncObjectUpdate message from {player.Username}, disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }
                
                if (UpdateUserSyncObject(message.Guid, message.Data, player))
                {
                    BroadcastGenericMessage(player, data, type, PacketType.ReliableGameData);
                }
                break;
            }
            
            case NetMessageType.SyncObjectCreate:
            {
                bool success = false;
                if (IsModNation)
                {
                    NetMessageSyncObject message;
                    try
                    {
                        message = NetworkReader.Deserialize<NetMessageSyncObject>(data);
                    }
                    catch (Exception)
                    {
                        Logger.LogWarning<GameSimulation>($"Failed to parse SyncObject message from {player.Username}, disconnecting them from the session.");
                        player.Disconnect();
                        break;
                    }
                    
                    switch (message.MessageType)
                    {
                        case NetObjectMessageType.Create:
                        {
                            success = PersistUserSyncedObject(new SyncObject(message, player.UserId), player);
                            break;
                        }
                        case NetObjectMessageType.Update:
                        {
                            success = UpdateUserSyncObject(message.Guid, message.Data, player);
                            break;
                        }
                        case NetObjectMessageType.Remove:
                        {
                            success = RemoveUserSyncObject(message.Guid, player);
                            break;
                        }
                    }
                }

                if (IsKarting)
                {
                    NetMessageSyncObjectCreate message;
                    try
                    {
                        message = NetworkReader.Deserialize<NetMessageSyncObjectCreate>(data);
                    }
                    catch (Exception)
                    {
                        Logger.LogWarning<GameSimulation>($"Failed to parse SyncObjectCreate message from {player.Username}, disconnecting them from the session.");
                        player.Disconnect();
                        break;
                    }
                    
                    success = PersistUserSyncedObject(new SyncObject(message, player.UserId), player);
                }

                // If the operation succeeded, send the sync object message to everyone else in the server.
                if (success)
                {
                    BroadcastGenericMessage(player, data, type, PacketType.ReliableGameData);
                }
                
                break;
            }

            default:
            {
                Logger.LogWarning<GameSimulation>("Unhandled network message type: " + type);

                byte[] b = new byte[data.Count];
                data.CopyTo(b, 0);
                File.WriteAllBytes(type.ToString(), b);

                break;
            }
        }
    }
    
    private void SimUpdate()
    {
        var room = _gameroomState.Value;
        var raceInfo = _raceInfo.Value;

        foreach (var player in _players)
        {
            if (!player.IsFakePlayer) continue;
            
            player.State.Flags = PlayerStateFlags.AllFlags;
            player.State.IsConnecting = false;
            player.HasSentRaceResults = true;
        }

        // If the race hasn't started update the gameroom's state based on 
        // the current players in the lobby.
        if (_raceSettings != null && room.State < RoomState.RaceInProgress)
        {
            int numReadyPlayers =
                _players.Count(x => (x.State.Flags & PlayerStateFlags.GameRoomReady) != 0);
            bool hasMinPlayers = numReadyPlayers >= _raceSettings.Value.MinHumans;
            
            if (hasMinPlayers && room.State == RoomState.WaitingMinPlayers)
                SetCurrentGameroomState(RoomState.Ready);
            // Karting doesn't seem to allow the owner to manually start the event.
            // 5 seconds seems like a reasonable amount of time for a "last call"
            else if (IsKarting && room.State == RoomState.Ready && _lastStateTime + 5000 < TimeHelper.LocalTime)
                SetCurrentGameroomState(RoomState.CountingDown);
            else if (!hasMinPlayers)
                SetCurrentGameroomState(RoomState.WaitingMinPlayers);
        }
        
        switch (room.State)
        {
            case RoomState.CountingDown:
            {
                float timeRemaining = (room.LoadEventTime - TimeHelper.LocalTime);
                if (timeRemaining > _raceConstants.GameRoomTimerRacerLock)
                {
                    // If someone has joined the gameroom before the racer lock, we should pause the countdown until
                    // they're finished connecting.
                    if (_players.Any(x => (x.State.Flags & PlayerStateFlags.GameRoomReady) == 0))
                    {
                        SetCurrentGameroomState(RoomState.CountingDownPaused);
                        break;
                    }
                }
                
                // Wait until the timer has finished counting down, then broadcast to everyone that the race is in progress
                if (TimeHelper.LocalTime >= room.LoadEventTime)
                {
                    SetCurrentGameroomState(RoomState.RaceInProgress);
                }

                break;
            }
            case RoomState.CountingDownPaused:
            {
                if (_players.All(x => (x.State.Flags & PlayerStateFlags.GameRoomReady) != 0))
                {
                    SetCurrentGameroomState(RoomState.CountingDown);
                }
                
                break;
            }
            case RoomState.DownloadingTracks:
            {
                if (_players.All(x => (x.State.Flags & PlayerStateFlags.DownloadedTracks) != 0))
                    SetCurrentGameroomState(RoomState.CountingDown);
                break;
            }
            case RoomState.RaceInProgress:
            {
                if (_waitingForPlayerNisEvents || _waitingForPlayerStartEvents)
                {
                    var racers = _players.Where(player => !player.IsSpectator).ToList();
                    
                    if (_waitingForPlayerNisEvents && racers.All(x => (x.State.Flags & PlayerStateFlags.ReadyForNis) != 0))
                    {
                        _waitingForPlayerNisEvents = false;
                        BroadcastGenericIntMessage(TimeHelper.LocalTime, NetMessageType.NisStart, PacketType.ReliableGameData);
                        _waitingForPlayerStartEvents = true;
                    }
                
                    if (_waitingForPlayerStartEvents && racers.All(x => (x.State.Flags & PlayerStateFlags.ReadyForEvent) != 0))
                    {
                        int countdown = TimeHelper.LocalTime + RaceConfig.Instance.EventCountdownTime;
                        BroadcastGenericIntMessage(countdown, NetMessageType.EventStart, PacketType.ReliableGameData);
                        _waitingForPlayerStartEvents = false;
                    }
                }

                if (raceInfo.RaceState == RaceState.WaitingForRaceEnd)
                { 
                    if (TimeHelper.LocalTime >= raceInfo.RaceEndServerTime && !_hasSentEventResults)
                    {
                        _hasSentEventResults = true;
                        string results = EventResult.Serialize(_eventResults);
                        _eventResults.Clear();
                        
                        string destination = IsModNation ? "destKartPark" : "destPod";
                        if (_raceSettings!.Value.AutoReset)
                            destination = "destGameroom";
                        
                        int postRaceDelay = _raceConstants.PostRaceTime;
                        BroadcastMessage(new NetMessageEventResults
                        {
                            SenderNameUid = NetworkMessages.SimServerUID,
                            Platform = Platform,
                            ResultsXml = results,
                            Destination = destination,
                            PostEventDelayTime = TimeHelper.LocalTime + postRaceDelay,
                            PostEventScreenTime = postRaceDelay
                        }, PacketType.ReliableGameData);
                        
                        if (IsKarting) UpdateVotePackage();

                        // Broadcast new seed for the next race
                        _seed = CryptoHelper.GetRandomSecret();
                        BroadcastMessage(new NetMessageRandomSeed { Seed = _seed }, PacketType.ReliableGameData);
                    }
                    
                }
                
                
                break;
            }
        }
    }

    public void Tick()
    {
        if (Type != ServerType.Competitive) return;
        SimUpdate();
    }
}