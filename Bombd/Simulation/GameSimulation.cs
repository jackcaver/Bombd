using System.Text;
using System.Xml.Serialization;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Serialization.Wrappers;
using Bombd.Types.GameBrowser;
using Bombd.Types.Network;
using Bombd.Types.Network.Messages;
using Bombd.Types.Network.Objects;

namespace Bombd.Simulation;

public class GameSimulation
{
    private readonly int _owner;
    private readonly List<GamePlayer> _players;
    private readonly Dictionary<int, PlayerState> _playerStates = new();
    private readonly Dictionary<int, PlayerInfo> _playerInfos = new();
    
    private readonly int _seed = CryptoHelper.GetRandomSecret();
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
        _owner = owner;
        
        Logger.LogInfo<GameSimulation>($"Starting Game Simulation ({type}:{platform})");
        
        if (Type == ServerType.KartPark)
        {
            _coiInfo = CreateSystemSyncObject(new CoiInfo(), NetObjectType.NetCoiInfoPackage);
        }
        
        if (Type == ServerType.Competitive)
        {
            _gameroomState = CreateSystemSyncObject(new GameroomState(Platform), NetObjectType.GameroomState);
            _raceInfo = CreateSystemSyncObject(new SpectatorInfo(Platform), NetObjectType.SpectatorInfo);
            _aiInfo = CreateSystemSyncObject(new AiInfo(Platform), NetObjectType.AiInfo);
            _startingGrid = CreateSystemSyncObject(new StartingGrid(), NetObjectType.StartingGrid);
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
            KartSpeedAccel = 0x30
        };

        _playerInfos[userId] = new PlayerInfo
        {
            Operation = 3,
            NetcodeUserId = userId,
            NetcodeGamePlayerId = playerId,
            PlayerConnectId = userId,
            GuestOfPlayerNameUid = CryptoHelper.GetRandomSecret(),
            IsGroupLeader = true,
            PlayerGroupId = CryptoHelper.GetRandomSecret(),
            NameUid = openuid,
            PlayerName = username,
            PodLocation = string.Empty
        };
        
        CreateGenericSyncObject(new PlayerConfig
        {
            Type = 0,
            NetcodeUserId = userId,
            CharCreationId = 0x132c,
            KartCreationId = 0x1324,
            UidName = openuid,
            Username = username,
            CharDataBlob = File.ReadAllBytes("Data/Blobs/Karting/CharDataBlob"),
            KartDataBlob = File.ReadAllBytes("Data/Blobs/Karting/KartDataBlob")
        }, NetObjectType.PlayerConfig, username, userId);
        
        CreateGenericSyncObject(new PlayerAvatar
        {
            OwnerName = username,
            SmileyDataBlob = File.ReadAllBytes("Data/Blobs/Karting/SmileyDataBlob"),
            FrownyDataBlob = File.ReadAllBytes("Data/Blobs/Karting/FrownyDataBlob")
        }, NetObjectType.PlayerAvatar, username, userId);
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
                    JoinStatus = 1,
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
                    JoinStatus = 1,
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
        
        // Tell everybody else in the lobby why we left...
        BroadcastMessage(player, new NetMessagePlayerLeave
        {
            PlayerName = player.Username,
            Reason = 0
        }, PacketType.ReliableGameData);
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
        if (state == RoomState.CountingDown)
        {
            UpdateAi();
            UpdateStartingGrid();
            
            // If the gameroom isn't loaded before it receives the player create info,
            // it won't ever update the racer state
            if (Platform == Platform.Karting)
            {
                // Send all player infos
                BroadcastMessage(new NetMessagePlayerCreateInfo {
                    Data = new List<PlayerInfo>(_playerInfos.Values)
                }, PacketType.ReliableGameData);   
            }
            
            room.LoadEventTime = TimeHelper.LocalTime + BombdConfig.Instance.GameroomCountdownTime;
            room.LockedForRacerJoinsValue = BombdConfig.Instance.GameroomRacerLockTime;
            room.LockedTimerValue = BombdConfig.Instance.GameroomTimerLockTime; 
        }
        else
        {
            room.LoadEventTime = 0;
            room.LockedTimerValue = 0.0f;
            room.LockedForRacerJoinsValue = 0.0f;
        }
        
        _gameroomState.Sync();
    }
    
    private GenericSyncObject<T> CreateSystemSyncObject<T>(T instance, NetObjectTypeInfo typeInfo) where T : INetworkWritable
    {
        return CreateGenericSyncObject(instance, typeInfo, NetworkMessages.SimServerName, -1);
    }
    
    private GenericSyncObject<T> CreateGenericSyncObject<T>(T instance, NetObjectTypeInfo typeInfo, string owner, int userId) where T : INetworkWritable
    {
        int type = typeInfo.ModnationTypeId;
        if (Platform == Platform.Karting)
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
    
    private void UpdateAi()
    {
        if (_raceSettings == null) return;
        
        var owner = _players.Find(x => x.UserId == _owner);
        string username = owner?.Username ?? string.Empty;

        int maxAi = _aiInfo.Value.DataSet.Length;
        int maxHumans = _raceSettings.Value.MaxHumans;
        int numAi = Math.Min(maxAi, maxHumans - _players.Count);
        
        _aiInfo.Value = _raceSettings.Value.AiEnabled ? 
            new AiInfo(Platform, username, numAi) : new AiInfo(Platform);
    }

    private void UpdateStartingGrid()
    {
        _startingGrid.Value.Clear();
        foreach (PlayerState state in _playerStates.Values) _startingGrid.Value.Add((int)state.NameUid);
        for (int i = 0; i < _aiInfo.Value.Count; ++i)
        {
            int nameUid = CryptoHelper.StringHash32(_aiInfo.Value.DataSet[i].UidName);
            _startingGrid.Value.Add(nameUid);
        }
        
        _startingGrid.Sync();
    }

    private SyncObject? GetOwnedSyncObject(int guid, GamePlayer player)
    {
        if (!_syncObjects.TryGetValue(guid, out SyncObject? syncObject))
        {
            Logger.LogWarning<GameSimulation>($"{player.Username} tried to perform an operation on a SyncObject that doesn't exist!");
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
        if (Platform == Platform.Karting) 
            return new NetMessageSyncObjectCreate(syncObject);
        
        return new NetMessageSyncObject(syncObject, NetObjectMessageType.Create);
    }

    private INetworkMessage MakeUpdateSyncObjectMessage(SyncObject syncObject)
    {
        if (Platform == Platform.Karting) 
            return new NetMessageSyncObjectUpdate(syncObject);
        
        return new NetMessageSyncObject(syncObject, NetObjectMessageType.Update);
    }

    private INetworkMessage MakeRemoveSyncObjectMessage(SyncObject syncObject)
    {
        if (Platform == Platform.Karting) 
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
    
    private bool UpdateUserSyncObject(int guid, ArraySegment<byte> data, GamePlayer player)
    {
        SyncObject? syncObject = GetOwnedSyncObject(guid, player);
        if (syncObject == null) return false;
        
        Logger.LogInfo<GameSimulation>($"Updating SyncObject({syncObject})");
        
        byte[] array = new byte[data.Count];
        data.CopyTo(array);
        syncObject.Data = new ArraySegment<byte>(array);
        
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
        if (type != NetMessageType.VoipPacket && type != NetMessageType.MessageUnreliableBlock)
        {
            Logger.LogDebug<GameSimulation>($"Received NetMessage {type} from {player.Username} ({(uint)player.UserId}:{(uint)player.PlayerId})");   
        }
        
        switch (type)
        {
            case NetMessageType.PlayerCreateInfo:
            {
                var message = NetworkReader.Deserialize<NetMessagePlayerCreateInfo>(data);
                message.Data[0].Operation = 3;
                _playerInfos[player.UserId] = message.Data[0];
                var msg = NetworkWriter.Serialize(message);
                BroadcastGenericMessage(msg, NetMessageType.PlayerCreateInfo, PacketType.ReliableGameData);
                
                break;
            }
            case NetMessageType.PlayerLeave:
            {
                var message = NetworkReader.Deserialize<NetMessagePlayerLeave>(data);
                message.PlayerName = player.Username;
                BroadcastMessage(message, PacketType.ReliableGameData);
                break;
            }
            case NetMessageType.GameroomReady:
            {
                if (player.UserId != _owner) break;
                SetCurrentGameroomState(RoomState.Ready);
                if (Platform == Platform.Karting)
                {
                    SetCurrentGameroomState(RoomState.CountingDown);   
                }
                break;
            }
            case NetMessageType.GameroomStopTimer:
            {
                if (player.UserId != _owner) break;
                SetCurrentGameroomState(RoomState.CountingDownPaused);
                break;
            }
            case NetMessageType.SpectatorInfo:
            {
                _raceInfo.Value = SpectatorInfo.ReadVersioned(data, Platform);
                break;
            }
            case NetMessageType.GameroomDownloadTracksComplete:
            {
                if (player.UserId != _owner) break;
                SetCurrentGameroomState(RoomState.CountingDown);
                break;
            }
            case NetMessageType.ReadyForEventStart:
            {
                _playerStates[player.UserId].ReadyForEvent = true;
                break;
            }
            case NetMessageType.ReadyForNisStart:
            {
                _playerStates[player.UserId].ReadyForNis = true;
                break;
            }
            case NetMessageType.GameroomRequestStartEvent:
            {
                // Only the host should be allowed to start the event I'm fairly sure
                if (player.UserId != _owner) break;
                SetCurrentGameroomState(RoomState.DownloadingTracks);
                break;
            }
            case NetMessageType.EventResultsPreliminary:
            {
                List<EventResult> results;
                try
                {
                    using NetworkReaderPooled reader = NetworkReaderPool.Get(data);
                    results = EventResult.Deserialize(reader.ReadString(reader.Capacity));
                }
                catch (Exception)
                {
                    Logger.LogWarning<GameSimulation>($"Failed to parse EventResultsPreliminary for {player.Username}");
                    break;
                }
                
                _eventResults.AddRange(results);
                break;
            }
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
                if (player.UserId != _owner) break;
                
                var settings = EventSettings.ReadVersioned(data, Platform);
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

        if (_raceSettings != null)
        {
            if (_raceSettings.Value.MinHumans > _players.Count)
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
                    _raceSettings!.Value.UpdateReason = 6;
                    _raceSettings!.Sync();
                    
                    SetCurrentGameroomState(RoomState.RaceInProgress);
                    _waitingForPlayerNisEvents = true;
                }

                break;
            }
            case RoomState.RaceInProgress:
            {
                if (_waitingForPlayerNisEvents && _playerStates.Values.All(x => x.ReadyForNis))
                {
                    _waitingForPlayerNisEvents = false;
                    BroadcastGenericIntMessage(TimeHelper.LocalTime, NetMessageType.NisStart, PacketType.ReliableGameData);
                    _waitingForPlayerStartEvents = true;
                }
                
                if (_waitingForPlayerStartEvents && _playerStates.Values.All(x => x.ReadyForEvent))
                {
                    int countdown = TimeHelper.LocalTime + BombdConfig.Instance.EventCountdownTime;
                    BroadcastGenericIntMessage(countdown, NetMessageType.EventStart, PacketType.ReliableGameData);
                    _waitingForPlayerStartEvents = false;
                }

                if (raceInfo.RaceState == RaceState.WaitingForRaceEnd && 
                    TimeHelper.LocalTime >= raceInfo.RaceEndServerTime && !_hasSentEventResults)
                {
                    _hasSentEventResults = true;
                    var xml = Encoding.ASCII.GetBytes(EventResult.Serialize(_eventResults));
                    BroadcastGenericMessage(xml, NetMessageType.EventResultsFinal, PacketType.ReliableGameData);
                }
                
                if (raceInfo.RaceState == RaceState.PostRace &&
                    TimeHelper.LocalTime >= raceInfo.PostRaceServerTime)
                {
                    _hasSentEventResults = false;
                    SetCurrentGameroomState(RoomState.Ready);
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