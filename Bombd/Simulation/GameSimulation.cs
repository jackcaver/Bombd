using System.Xml.Serialization;
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
    private readonly Dictionary<int, PlayerState> _playerStates = new();
    private readonly Dictionary<int, PlayerInfo> _playerInfos = new();
    
    private int _seed = CryptoHelper.GetRandomSecret();
    private int _lastStateTime = TimeHelper.LocalTime;
    private readonly XmlSerializer _stateSerializer = new(typeof(PlayerState));
    private readonly Dictionary<int, SyncObject> _syncObjects = new();
    
    public readonly Platform Platform;
    public readonly ServerType Type;
    
    private GenericSyncObject<CoiInfo> _coiInfo;
    private GenericSyncObject<GameroomState> _gameroomState;
    private GenericSyncObject<AiInfo> _aiInfo;
    private GenericSyncObject<SpectatorInfo> _raceInfo;
    private GenericSyncObject<StartingGrid> _startingGrid;
    private GenericSyncObject<EventSettings>? _raceSettings;
    private List<EventResult> _eventResults = new();

    private bool _waitingForPlayerNisEvents;
    private bool _waitingForPlayerStartEvents;
    private bool _hasSentEventResults;

    public GameSimulation(ServerType type, Platform platform, int owner, List<GamePlayer> players)
    {
        Type = type;
        Platform = platform;
        _players = players;
        Owner = owner;
        
        // MODNATION ITEM USAGE
            // When player first starts race, they request ArbitratedItemCreateBlock
                // Not sure if there's an error response or if this should be sent to everybody in the server
                // but just send it back to the player
            // When player goes over an item, they request ArbitratedItemAcquire
                // 
        
        // upon having minimum number of players
        // it seems to wait 3 or 5 seconds until starting the timer
        // timer lock is 55 seconds elapsed (when timer hits 5) this might also be the player lock?
        // pregame timer in karting is 60 seconds
        // voting time should be 45 seconds it seems
        // after voting thrown into another pre game lobby
        
        
        
        Logger.LogInfo<GameSimulation>($"Starting Game Simulation ({type}:{platform})");
        
        if (Type == ServerType.KartPark)
        {
            _coiInfo = CreateSystemSyncObject(WebApiManager.GetCircleOfInfluence(), NetObjectType.NetCoiInfoPackage);
        }

        if (Type == ServerType.Competitive)
        {
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
        
        var player = new GamePlayer
        {
            IsFakePlayer = true,
            Platform = Platform,
            PlayerId = playerId,
            Send = (data, type) => { },
            Disconnect = { },
            UserId = userId,
            Username = username
        };
        
        _players.Add(player);

        _playerStates[userId] = new PlayerState
        {
            PlayerConnectId = (uint)userId,
            NameUid = (uint)nameUid,
            KartId = 0x1324,
            CharacterId = 0x132c,
            KartSpeedAccel = 0x0
        };

        _playerInfos[userId] = new PlayerInfo
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
    public bool IsModnation => Platform == Platform.ModNation;
    public bool HasRaceSettings => _raceSettings != null;
    
    public void OnPlayerJoin(GamePlayer player)
    {
        // Don't actually know if this is random per room or random per player,
        // I assume it's used for determinism, but I don't know?
        player.SendReliableMessage(new NetMessageRandomSeed { Seed = _seed });
        
        if (IsKarting)
        {
            // Send all player infos
            player.SendReliableMessage(new NetMessagePlayerCreateInfo
            {
                Data = new List<PlayerInfo>(_playerInfos.Values)
            });
        }
        else
        {
            // Tell everybody else about yourself
            foreach (GamePlayer peer in _players)
            {
                peer.SendReliableMessage(new NetMessagePlayerSessionInfo
                {
                    JoinStatus = GameSessionStatus.JoinAsRacer,
                    PlayerId = player.PlayerId,
                    UserId = player.UserId
                });
            }

            // Tell the player about everyone who is in the game.
            foreach (GamePlayer peer in _players)
            {
                if (player == peer) continue;
                player.SendReliableMessage(new NetMessagePlayerSessionInfo
                {
                    JoinStatus = GameSessionStatus.JoinAsRacer,
                    PlayerId = peer.PlayerId,
                    UserId = peer.UserId
                });
            }   
        }
        

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
        // Make sure we're not still keeping track of the player's state even after they left.
        _playerStates.Remove(player.UserId);

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
        
        if (_players.Count == 0) return;
        
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
            BroadcastGenericIntMessage(randomPlayer.UserId, NetMessageType.LeaderChangeRequest, PacketType.ReliableGameData);
        }
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
        Logger.LogInfo<GameSimulation>("Player state update received. Broadcasting new states to room.");
        foreach (GamePlayer player in _players)
        {
            bool hasState = _playerStates.TryGetValue(player.UserId, out PlayerState? state);
            if (hasState) Logger.LogDebug<GameSimulation>($" -> {player.Username} : {state}");
            else Logger.LogDebug<GameSimulation>($" -> {player.Username} : NO STATE");
        }
        
        if (IsKarting)
        {
            BroadcastMessage(new NetMessageKartingPlayerUpdate { StateUpdates = _playerStates.Values }, PacketType.ReliableGameData);
            return;
        }
        
        BroadcastMessage(new NetMessageModnationPlayerUpdate { StateUpdates = _playerStates.Values }, PacketType.ReliableGameData);
    }
    
    private void SetCurrentGameroomState(RoomState state)
    {
        var room = _gameroomState.Value;
        if (state == room.State) return;
        
        Logger.LogInfo<GameSimulation>($"Setting GameRoomState to {state}");
        
        room.State = state;
        room.LoadEventTime = 0;
        room.LockedTimerValue = 0.0f;
        room.LockedForRacerJoinsValue = 0.0f;
        
        if (state <= RoomState.Ready || state == RoomState.RaceInProgress)
        {
            // Reset the race loading flags for each player
            foreach (var playerState in _playerStates.Values)
            {
                playerState.Flags &= ~PlayerStateFlags.RaceLoadFlags;
            }
        }
        
        switch (state)
        {
            case RoomState.None:
            { 
                _waitingForPlayerNisEvents = false; 
                _waitingForPlayerStartEvents = false; 
                _hasSentEventResults = false;
                foreach (var playerState in _playerStates.Values)
                    playerState.Flags = PlayerStateFlags.None;
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
                if (IsKarting)
                {
                    foreach (var info in _playerInfos.Values)
                    {
                        info.Operation = GameJoinStatus.RacerPending;
                    }
                    
                    // Send all player infos
                    BroadcastMessage(new NetMessagePlayerCreateInfo {
                        Data = new List<PlayerInfo>(_playerInfos.Values)
                    }, PacketType.ReliableGameData);   
                }
                
                Logger.LogDebug<GameSimulation>("Room has readied up, current state is as follows:");
                foreach (var player in _players)
                {
                    Logger.LogDebug<GameSimulation>($"{player.Username}: UserId={player.UserId},PlayerId={player.PlayerId}");
                    if (player.Guest != null)
                        Logger.LogDebug<GameSimulation>($"  -> {player.Guest.Username}");
                }
                
                break;
            }
            case RoomState.CountingDown:
            {
                UpdateRaceSetup();
                
                if (Platform == Platform.ModNation)
                {
                    room.LoadEventTime = TimeHelper.LocalTime + BombdConfig.Instance.ModnationGameroomCountdownTime;
                    room.LockedForRacerJoinsValue = BombdConfig.Instance.ModnationGameroomRacerLockTime;
                    room.LockedTimerValue = BombdConfig.Instance.ModnationGameroomTimerLockTime;   
                }
                else
                {
                    room.LoadEventTime = TimeHelper.LocalTime + BombdConfig.Instance.KartingGameroomCountdownTime;
                    room.LockedForRacerJoinsValue = BombdConfig.Instance.KartingGameroomTimerLock;
                    room.LockedTimerValue = BombdConfig.Instance.KartingGameroomTimerLock;
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
            if (!_playerStates.TryGetValue(player.UserId, out PlayerState? state)) continue;
            _startingGrid.Value.Add(new GridPositionData((int)state.NameUid, false));
            if (player.Guest != null)
                _startingGrid.Value.Add(new GridPositionData(player.Guest.NameUid, true));
        }
        
        var owner = _players.Find(x => x.UserId == Owner);
        string username = owner?.Username ?? string.Empty;

        int maxAi = _aiInfo.Value.DataSet.Length;
        int maxHumans = _raceSettings.Value.MaxHumans;
        int numAi = Math.Min(maxAi, maxHumans - _startingGrid.Value.Count);
        if (numAi <= 0) numAi = 0;
        
        _aiInfo.Value = _raceSettings.Value.AiEnabled ? 
            new AiInfo(Platform, username, numAi) : new AiInfo(Platform);

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

    private void ParseGuestInfo(ArraySegment<byte> data, GamePlayer player)
    {
        if (player.Guest == null) return;
        try
        {
            var config = PlayerConfig.ReadVersioned(data, Platform);
            
            // Type 1 is guest
            if (config.Type != 1) return;
            if ((IsKarting && config.NetcodeUserId != -1) || (IsModnation && config.NetcodeUserId != player.UserId))
            {
                Logger.LogWarning<GameSimulation>($"Guest doesn't belong to {player.Username}, this shouldn't happen, disconnecting!");
                player.Disconnect();
                return;
            }
            
            player.Guest.NameUid = CryptoHelper.StringHash32(config.UidName);
        }
        catch (Exception)
        {
            Logger.LogError<GameSimulation>($"Failed to parse PlayerConfig for {player.Username}'s guest, disconnecting them from the game session.");
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
        
        // If it's a player config object, pull the guest name uid from it if possible
        var playerConfigType = NetObjectType.PlayerConfig;
        if ((IsModnation && syncObject.Type == playerConfigType.ModnationTypeId) ||
                                     (IsKarting && syncObject.Type == playerConfigType.KartingTypeId))
        {
            ParseGuestInfo(data, player);
        }
        
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
            type != NetMessageType.GenericGameplay &&
            type != NetMessageType.Gameplay &&
            type != NetMessageType.SpectatorInfo)
        {
            Logger.LogDebug<GameSimulation>($"Received NetMessage {type} from {player.Username} ({(uint)player.UserId}:{(uint)player.PlayerId})");   
        }
        
        switch (type)
        {
            case NetMessageType.ItemMessage_0x10:
            case NetMessageType.ItemDestroy:
            case NetMessageType.ItemHitPlayer:
            case NetMessageType.ItemHitConfirm:
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
                var message = NetworkReader.Deserialize<NetMessagePlayerCreateInfo>(data);
                // message.Data[0].Operation = GameJoinStatus.RacerPending;
                _playerInfos[player.UserId] = message.Data[0];
                var msg = NetworkWriter.Serialize(message);
                BroadcastGenericMessage(player, msg, NetMessageType.PlayerCreateInfo, PacketType.ReliableGameData);
                break;
            }
            case NetMessageType.WorldobjectCreate:
            {
                BroadcastGenericMessage(data, NetMessageType.WorldobjectCreate, PacketType.ReliableGameData);
                break;
            }
            case NetMessageType.PlayerLeave:
            {
                var message = NetMessagePlayerLeave.ReadVersioned(data, Platform);
                message.PlayerName = player.Username;
                BroadcastMessage(message, PacketType.ReliableGameData);
                break;
            }
            case NetMessageType.GameroomReady:
            {
                _playerStates[player.UserId].Flags |= PlayerStateFlags.GameRoomReady;
                
                // Once the player has finished loading, we should now send them their session info
                if (Platform == Platform.Karting)
                {
                    UpdateRaceSetup();
                    var info = _playerInfos[player.UserId];
                    info.Operation = GameJoinStatus.RacerPending;
                    BroadcastMessage(new NetMessagePlayerCreateInfo
                    {
                        Data = new List<PlayerInfo> { info }
                    }, PacketType.ReliableGameData);
                }
                
                break;
            }
            case NetMessageType.GameroomStopTimer:
            {
                if (player.UserId != Owner) break;
                SetCurrentGameroomState(RoomState.CountingDownPaused);
                break;
            }
            case NetMessageType.SpectatorInfo:
            {
                if (player.UserId != Owner) break;
                _raceInfo.Value = SpectatorInfo.ReadVersioned(data, Platform);
                break;
            }
            case NetMessageType.GameroomDownloadTracksComplete:
            {
                _playerStates[player.UserId].Flags |= PlayerStateFlags.DownloadedTracks;
                break;
            }
            case NetMessageType.ReadyForEventStart:
            {
                _playerStates[player.UserId].Flags |= PlayerStateFlags.ReadyForEvent;
                break;
            }
            case NetMessageType.ReadyForNisStart:
            {
                _playerStates[player.UserId].Flags |= PlayerStateFlags.ReadyForNis;
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
                    Logger.LogWarning<GameSimulation>($"Failed to parse EventResultsPreliminary for {player.Username}");
                    break;
                }
                
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
                
                var settings = EventSettings.ReadVersioned(data, Platform);
                
                // This is mostly only used for debugging purposes.
                if (!BombdConfig.Instance.EnforceMinimumRacerRequirement)
                    settings.MinHumans = 1;
                
                if (_raceSettings != null) _raceSettings.Value = settings;
                else _raceSettings = CreateSystemSyncObject(settings, NetObjectType.RaceSettings);
                
                Logger.LogDebug<GameSimulation>("RaceType: " + _raceSettings.Value.RaceType);
                Logger.LogDebug<GameSimulation>("Speed: " + _raceSettings.Value.KartSpeed);
                
                
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
                if (IsKarting)
                {
                    var message = NetworkReader.Deserialize<NetMessageKartingPlayerUpdate>(data);
                    state = message.StateUpdates.ElementAt(0);
                }
                else
                {
                    try
                    {
                        using NetworkReaderPooled reader = NetworkReaderPool.Get(data);
                        using var stringReader = new StringReader(reader.ReadString(reader.Capacity));
                        state = (PlayerState)_stateSerializer.Deserialize(stringReader)!;
                    }
                    catch (Exception)
                    {
                        Logger.LogWarning<GameSimulation>($"Failed to parse playerStateUpdate for {player.Username}, disconnecting them from the session.");
                        player.Disconnect();
                        break;
                    }
                }
                
                // Backup user flags
                if (_playerStates.TryGetValue(player.UserId, out PlayerState? existingPlayerState))
                    state.Flags = existingPlayerState.Flags;
                state.NetcodeUserId = player.UserId;
                _playerStates[player.UserId] = state;
                
                BroadcastPlayerStates();
            
                break;
            }

            case NetMessageType.SyncObjectRemove:
            {
                var message = NetworkReader.Deserialize<NetMessageSyncObjectRemove>(data);
                if (RemoveUserSyncObject(message.Guid, player))
                {
                    BroadcastGenericMessage(player, data, type, PacketType.ReliableGameData);
                }
                break;
            }
            
            case NetMessageType.SyncObjectUpdate:
            {
                var message = NetworkReader.Deserialize<NetMessageSyncObjectUpdate>(data);
                
                if (UpdateUserSyncObject(message.Guid, message.Data, player))
                {
                    BroadcastGenericMessage(player, data, type, PacketType.ReliableGameData);
                }
                break;
            }
            
            case NetMessageType.SyncObjectCreate:
            {
                bool success = false;
                if (IsModnation)
                {
                    var message = NetworkReader.Deserialize<NetMessageSyncObject>(data);
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
                    var message = NetworkReader.Deserialize<NetMessageSyncObjectCreate>(data);
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
            if (player.IsFakePlayer)
                _playerStates[player.UserId].Flags = PlayerStateFlags.AllFlags;
        }

        // If the race hasn't started update the gameroom's state based on 
        // the current players in the lobby.
        if (_raceSettings != null && room.State < RoomState.RaceInProgress)
        {
            int numReadyPlayers =
                _playerStates.Values.Count(x => (x.Flags & PlayerStateFlags.GameRoomReady) != 0 && x.Away == 0);
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
        
        // TODO: Exclude players who joined after the countdown
        switch (room.State)
        {
            case RoomState.CountingDown:
            {
                // Wait until the timer has finished counting down, then broadcast to everyone that the race is in progress
                if (TimeHelper.LocalTime >= room.LoadEventTime)
                {
                    SetCurrentGameroomState(RoomState.RaceInProgress);
                }

                break;
            }
            case RoomState.DownloadingTracks:
            {
                if (_playerStates.Values.All(x => (x.Flags & PlayerStateFlags.DownloadedTracks) != 0))
                    SetCurrentGameroomState(RoomState.CountingDown);
                break;
            }
            case RoomState.RaceInProgress:
            {
                if (_waitingForPlayerNisEvents && 
                    _playerStates.Values.All(x => (x.Flags & PlayerStateFlags.ReadyForNis) != 0))
                {
                    _waitingForPlayerNisEvents = false;
                    BroadcastGenericIntMessage(TimeHelper.LocalTime, NetMessageType.NisStart, PacketType.ReliableGameData);
                    _waitingForPlayerStartEvents = true;
                }
                
                if (_waitingForPlayerStartEvents && 
                    _playerStates.Values.All(x => (x.Flags & PlayerStateFlags.ReadyForEvent) != 0))
                {
                    int countdown = TimeHelper.LocalTime + BombdConfig.Instance.EventCountdownTime;
                    BroadcastGenericIntMessage(countdown, NetMessageType.EventStart, PacketType.ReliableGameData);
                    _waitingForPlayerStartEvents = false;
                }

                if (raceInfo.RaceState == RaceState.WaitingForRaceEnd && 
                    TimeHelper.LocalTime >= raceInfo.RaceEndServerTime && !_hasSentEventResults)
                {
                    _hasSentEventResults = true;

                    string destination = IsModnation ? "destKartPark" : "destPod";
                    if (_raceSettings!.Value.AutoReset)
                        destination = "destGameroom";
                    
                    BroadcastMessage(new NetMessageEventResults
                    {
                        SenderNameUid = NetworkMessages.SimServerUID,
                        Platform = Platform,
                        ResultsXml = EventResult.Serialize(_eventResults),
                        Destination = destination,
                        PostEventDelayTime = TimeHelper.LocalTime + 15000,
                        PostEventScreenTime = 15.0f
                    }, PacketType.ReliableGameData);

                    // Broadcast new seed for the next race
                    _seed = CryptoHelper.GetRandomSecret();
                    BroadcastMessage(new NetMessageRandomSeed { Seed = _seed }, PacketType.ReliableGameData);
                    
                    SetCurrentGameroomState(RoomState.None);
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