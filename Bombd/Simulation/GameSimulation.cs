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

    private readonly int _seed = CryptoHelper.GetRandomSecret();
    private readonly XmlSerializer _stateSerializer = new(typeof(PlayerState));
    private readonly Dictionary<int, SyncObject> _syncObjects = new();
    
    public readonly Platform Platform;
    public readonly ServerType Type;
    
    private GenericSyncObject<CoiInfo> CoiInfo;
    private GenericSyncObject<GameroomState> GameroomState;
    private GenericSyncObject<AiInfo> AiInfo;
    private GenericSyncObject<SpectatorInfo> SpectatorInfo;
    private GenericSyncObject<StartingGrid> StartingGrid;
    private GenericSyncObject<EventSettings>? RaceSettings;
    private List<EventResult> EventResults = new();
    
    private bool _waitingForPlayerNisEvents;
    private bool _waitingForPlayerStartEvents;
    private bool _hasSentEventResults;

    public GameSimulation(ServerType type, Platform platform, int owner, List<GamePlayer> players)
    {
        Type = type;
        Platform = platform;
        _players = players;
        _owner = owner;
        
        Logger.LogInfo<GameSimulation>($"Starting SimServer with Type = {Type}, Platform = {platform}");
        if (Type == ServerType.KartPark)
        {
            CoiInfo = CreateSystemSyncObject(new CoiInfo(), NetObjectType.NetCoiInfoPackage);
        }
        else if (Type == ServerType.Competitive)
        {
            SpectatorInfo = CreateSystemSyncObject(new SpectatorInfo(), NetObjectType.SpectatorInfo);
            AiInfo = CreateSystemSyncObject(new AiInfo(string.Empty, 0), NetObjectType.AiInfo);
            GameroomState = CreateSystemSyncObject(new GameroomState(), NetObjectType.GameroomState);
            StartingGrid = CreateSystemSyncObject(new StartingGrid(), NetObjectType.StartingGrid);
        }
    }

    public bool IsKarting => Platform == Platform.Karting;
    public bool IsModnation => Platform == Platform.ModNation;
    public bool HasRaceSettings => RaceSettings != null;
    
    public void OnPlayerJoin(GamePlayer player)
    {
        // Don't actually know if this is random per room or random per player,
        // I assume it's used for determinism, but I don't know?
        player.SendReliableMessage(new NetMessageRandomSeed { Seed = _seed });

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
        BroadcastMessage(new NetMessageBulkPlayerStateUpdate { StateUpdates = _playerStates.Values }, PacketType.ReliableGameData);
    }
    
    private void SetCurrentGameroomState(RoomState state)
    {
        Logger.LogInfo<GameSimulation>($"Setting GameRoomState to {state}");
        GameroomState.Value.State = state;
        GameroomState.Sync();
    }

    private GenericSyncObject<T> CreateSystemSyncObject<T>(T instance, int type) where T : INetworkWritable
    {
        var syncObject = new GenericSyncObject<T>(instance, type);
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
        if (RaceSettings == null) return;
        
        var owner = _players.Find(x => x.UserId == _owner);
        string username = owner?.Username ?? string.Empty;

        int maxAi = AiInfo.Value.DataSet.Length;
        int maxPlayers = RaceSettings.Value.MaxPlayers;
        
        int numAi = Math.Min(maxAi, maxPlayers - _players.Count);
        AiInfo.Value = RaceSettings.Value.AiEnabled ? new AiInfo(username, numAi) : new AiInfo(username, 0);
    }

    private void UpdateStartingGrid()
    {
        StartingGrid.Value.Clear();
        foreach (PlayerState state in _playerStates.Values) StartingGrid.Value.Add((int)state.NameUid);
        for (int i = 0; i < AiInfo.Value.Count; ++i)
        {
            int nameUid = CryptoHelper.StringHash32(AiInfo.Value.DataSet[i].UidName);
            StartingGrid.Value.Add(nameUid);
        }
        
        StartingGrid.Sync();
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
            $"Creating SyncObject({syncObject}) with owner {syncObject.OwnerName}");
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
    
    public void OnNetworkMessage(GamePlayer player, NetMessageType type, ArraySegment<byte> data)
    {
        if (type != NetMessageType.VoipPacket && type != NetMessageType.MessageUnreliableBlock)
        {
            Logger.LogDebug<GameSimulation>($"Received NetMessage {type} from {player.Username} ({(uint)player.UserId}:{(uint)player.PlayerId})");   
        }

        switch (type)
        {
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
                SpectatorInfo.Value = NetworkReader.Deserialize<SpectatorInfo>(data);
                break;
            }
            case NetMessageType.GameroomDownloadTracksComplete:
            {
                if (player.UserId != _owner) break;
                
                GameroomState.Value.LoadEventTime = TimeHelper.LocalTime + BombdConfig.Instance.GameroomCountdownTime;
                GameroomState.Value.LockedForRacerJoinsValue = BombdConfig.Instance.GameroomRacerLockTime;
                GameroomState.Value.LockedTimerValue = BombdConfig.Instance.GameroomTimerLockTime;
                
                SetCurrentGameroomState(RoomState.CountingDown);
                UpdateAi();
                UpdateStartingGrid();
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
                
                EventResults.AddRange(results);
                break;
            }
            case NetMessageType.PlayerFinishedEvent:
            case NetMessageType.GenericMessage0Xe:
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
                
                var settings = NetworkReader.Deserialize<EventSettings>(data);
                if (RaceSettings != null) RaceSettings.Value = settings;
                else RaceSettings = CreateSystemSyncObject(settings, NetObjectType.RaceSettings);
                
                break;
            }
            case NetMessageType.VoipPacket:
            {
                BroadcastVoipData(player, data);
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
                if (IsModnation)
                {
                    PlayerState state;
                    try
                    {
                        using NetworkReaderPooled reader = NetworkReaderPool.Get(data);
                        using var stringReader = new StringReader(reader.ReadString(reader.Capacity));
                        state = (PlayerState)_stateSerializer.Deserialize(stringReader)!;
                    }
                    catch (Exception)
                    {
                        // TODO: Disconnect the user since this shouldn't ever happen
                        // with normal gamedata.
                        Logger.LogWarning<GameSimulation>($"Failed to parse playerStateUpdate for {player.Username}");
                        break;
                    }

                    _playerStates[player.UserId] = state;
                    BroadcastPlayerStates();
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

    private void TickGameRoom()
    {
        var room = GameroomState.Value;
        var raceInfo = SpectatorInfo.Value;
        
        // TODO: Exclude players who joined after the countdown
        switch (room.State)
        {
            case RoomState.CountingDown:
            {
                // Wait until the timer has finished counting down, then broadcast to everyone that the race is in progress
                if (TimeHelper.LocalTime >= room.LoadEventTime)
                {
                    room.LoadEventTime = 0;
                    room.LockedTimerValue = 0.0f;
                    room.LockedForRacerJoinsValue = 0.0f;
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
                    var xml = Encoding.ASCII.GetBytes(EventResult.Serialize(EventResults));
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
        if (Type == ServerType.Competitive) TickGameRoom();
    }
}