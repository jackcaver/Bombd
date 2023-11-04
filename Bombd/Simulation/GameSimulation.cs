using System.Text;
using System.Xml.Serialization;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Serialization.Wrappers;
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
    private readonly GameroomState GameroomState = new();

    public readonly Platform Platform;
    public readonly ServerType Type;
    
    private SpectatorInfo SpectatorInfo = new();
    private StartingGrid StartingGrid = new();
    private AiInfo AiInfo = new(string.Empty, 0);
    private EventSettings? RaceSettings;
    private List<EventResult> EventResults = new();
    
    private bool WaitingForPlayerNisEvents;
    private bool WaitingForPlayerStartEvents;
    private bool SentEventResults;

    public GameSimulation(ServerType type, Platform platform, int owner, List<GamePlayer> players)
    {
        Type = type;
        Platform = platform;
        _players = players;
        _owner = owner;

        Logger.LogInfo<GameSimulation>($"Starting SimServer with Type = {Type}, Platform = {platform}");


        // COMPETITIVE RACE FLOW - SENDER UID = DCE7631C, USER ID = 0xAE967D6D - 4 PLAYER RACE WITH AI, 2 HUMANS, 2 AI
        // S CreateSyncObject SpectactorInfo 0x221c7baf
        // S UpdateSyncObject SpectatorInfo ; 0 bytes
        // S CreateSyncObject SimServerAIInfo 
        // S UpdateSyncObject SimServerAIInfo ; Contains no AI
        // C PlayerStateUpdate ; Owner updates their player state
        // C EventSettingsUpdate ; Owner transmits their race settings
        // S UpdateSyncObject SimServerAIInfo ; Reflects the AI settings set by race settings? I think still has to contain no AI
        // S CreateSyncObject GameroomState 0x221C7BB1
        // S UpdateSyncObject GameroomState ; Default gameroom state, basically null bytes, state = 0
        // S CreateSyncObject StartingGrid 0x221c7bb3
        // S UpdateSyncObject StartingGrid ; Contains no racers
        // S UpdateSyncObject GameroomState ; Sets the last integer field to 0x00005404? Not sure what this is. Could be session uuid?
        // S CreateSyncObject raceSettings 0x221c7bef
        // S UpdateSyncObject raceSettings ; Basically what was sent in the event settings update, except value at 0x88 is set to 1? Generic04
        // S UpdateSyncObject GameroomState ; Sets state to WaitingForMinPlayers if applicable, last integer field is 0x4c04 now?
        // S UpdateSyncObject GameroomState ; Last integer field is 0x0 now?
        // C SpectatorInfo ; Default spectator info
        // C CreateSyncObject Owner ; Owner sends their player object to the server
        // C UpdateSyncObject Owner ; Updates with character/kart data
        // S UpdateSyncedObject SpectatorInfo ; Updates with data that was received from client earlier
        // C GameroomReady ; Owner says that they're ready (unknown if this is because there's enough players or just to say the owner is ready)
        // C GameroomRequestStartEvent ; Owner wants to start the event
        // S UpdateSyncObject GameroomState ; Sets state to DownloadingTracks, last integer is 0x5404?
        // C GameroomDownloadTracksComplete ; Owner tells server that they finished downloading tracks
        // Wait until this message is received from all players before continuing.
        // S UpdateSyncObject GameroomState ; Sets state to CountingDown, sets LoadEventTime to when event should start (30 seconds from now), sets LockedForRacerJoinsValue to 25000.0f, LockedTimerValue = 20000.0f
        // S UpdateSyncObject SimServerAIInfo ; Contains AI relevant to the race settings (2 ai in this case, 
        // S UpdateSyncObject StartingGrid ; Contains all players/ai now in the race, uses username uids for players, string hash of uid name for ai
        // S UpdateSyncObject raceSettings ; Value at 0x88 set to 9? Generic09
        // S UpdateSyncObject GameroomState ; Don't think it's any different from the previous one
        // S UpdateSyncObject GameroomState ; Clears lock times, sets state to RaceInProgress (The countdown is over)
        // C ArbitratedItemCreate
        // S ArbitratedItemCreate ; Basically sends back exactly what the client sent it a second ago
        // C ArbitratedItemCreate
        // S ArbitratedItemCreate ; Basically sends back exactly what the client sent it a second ago
        // C ArbitratedItemCreateBlock
        // C ReadyForNisStart ; Owner tells server they finished loading and are ready for the intro movie
        // Wait until this message is received from all players before continuing.
        // S UpdateSyncObject GameroomState ; Last value set to 0x6104?
        // S NisStart ;  Tells client that they can begin the video, includes value for current timestamp?
        // S UpdateSyncObject GameroomState ; Last value set to 0x5404?
        // The 0xE packets should start being broadcasted periodically, assumed to be GenericGameplay?
        // C ReadyForEventStart ; Owner tells server they finished watching the intro movie
        // Wait until this message is received from all players before continuing.
        // S UpdateSyncObject GameroomState ; Last value set to 0x6104?
        // S EventStart ; Tells client that the race has begun, includes timestamp for start time


        // AFTER RACE

        // C PlayerFinishedRace (Broadcast?)
        // C EventResultsPreliminary

        if (Type == ServerType.KartPark)
        {
            _syncObjects[NetObjectType.NetCoiInfoPackage] = new SyncObject
            {
                OwnerName = "simserver",
                DebugTag = "CoiInfo",
                Guid = NetObjectType.NetCoiInfoPackage,
                Type = NetObjectType.NetCoiInfoPackage,
                Data = NetworkWriter.Serialize(new CoiInfo())
            };
        }

        if (Type == ServerType.Competitive)
        {
            _syncObjects[NetObjectType.SpectatorInfo] = new SyncObject
            {
                OwnerName = "simserver",
                DebugTag = "SpectatorInfo",
                Guid = NetObjectType.SpectatorInfo,
                Type = NetObjectType.SpectatorInfo,
                Data = NetworkWriter.Serialize(SpectatorInfo)
            };

            _syncObjects[NetObjectType.AiInfo] = new SyncObject
            {
                OwnerName = "simserver",
                DebugTag = "SimServerAIInfo",
                Guid = NetObjectType.AiInfo,
                Type = NetObjectType.AiInfo,
                Data = NetworkWriter.Serialize(AiInfo)
            };

            _syncObjects[NetObjectType.ModnationGameroomState] = new SyncObject
            {
                OwnerName = "simserver",
                DebugTag = "GameroomState",
                Guid = NetObjectType.ModnationGameroomState,
                Type = NetObjectType.ModnationGameroomState,
                Data = NetworkWriter.Serialize(GameroomState)
            };

            _syncObjects[NetObjectType.ModnationStartingGrid] = new SyncObject
            {
                OwnerName = "simserver",
                DebugTag = "StartingGrid",
                Guid = NetObjectType.ModnationStartingGrid,
                Type = NetObjectType.ModnationStartingGrid,
                Data = NetworkWriter.Serialize(StartingGrid)
            };

            _syncObjects[NetObjectType.RaceSettings] = new SyncObject
            {
                OwnerName = "simserver",
                DebugTag = "raceSettings",
                Guid = NetObjectType.RaceSettings,
                Type = NetObjectType.RaceSettings
            };
        }
    }

    public bool IsKarting => Platform == Platform.Karting;
    public bool IsModnation => Platform == Platform.ModNation;

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
            var message = new NetMessageSyncObjectCreate
            {
                MessageType = NetObjectMessageType.Create,
                DebugTag = syncObject.DebugTag,
                Guid = syncObject.Guid,
                ObjectType = syncObject.Type,
                OwnerName = syncObject.OwnerName
            };

            // Needs to be sent twice with a create and update message,
            // they use the same net message type in Modnation despite having
            // separate network event types, but whatever.
            player.SendReliableMessage(message);

            // If the segment is empty, it probably hasn't been initialized
            // by the owner yet, don't send it.
            if (syncObject.Data.Count != 0)
            {
                message.Data = syncObject.Data;
                message.MessageType = NetObjectMessageType.Update;
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
            var message = new NetMessageSyncObjectCreate
            {
                MessageType = NetObjectMessageType.Remove,
                Guid = pair.Key
            };

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

    public void BroadcastVoipData(GamePlayer sender, ArraySegment<byte> data)
    {
        foreach (var player in _players)
        {
            if (player == sender) continue;
            player.Send(data, PacketType.VoipData);
        }
    }
    
    public void BroadcastGenericIntMessage(int value, NetMessageType messageType, PacketType packetType)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> message = NetworkMessages.PackInt(writer, value, messageType);
        foreach (GamePlayer player in _players)
        {
            player.Send(message, packetType);
        }
    }
    
    public void BroadcastGenericMessage(GamePlayer sender, ArraySegment<byte> data, NetMessageType messageType, PacketType packetType)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> message = NetworkMessages.PackData(writer, data, messageType);
        foreach (GamePlayer player in _players)
        {
            if (player == sender) continue;
            player.Send(message, packetType);
        }
    }
    
    public void BroadcastGenericMessage(ArraySegment<byte> data, NetMessageType messageType, PacketType packetType)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> message = NetworkMessages.PackData(writer, data, messageType);
        foreach (GamePlayer player in _players)
        {
            player.Send(message, packetType);
        }
    }

    public void BroadcastMessage(GamePlayer sender, INetworkMessage message, PacketType type)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> payload = NetworkMessages.Pack(writer, message);
        foreach (GamePlayer player in _players)
        {
            if (player == sender) continue;
            player.Send(payload, type);
        }
    }
    
    public void BroadcastMessage(INetworkMessage message, PacketType type)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> payload = NetworkMessages.Pack(writer, message);
        foreach (GamePlayer player in _players) player.Send(payload, type);
    }
    
    public void BroadcastPlayerStates()
    {
        Logger.LogInfo<GameSimulation>("Broadcasting bulk state update to all players in game room");

        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> message = NetworkMessages.Pack(writer, new NetMessageBulkPlayerStateUpdate
        {
            StateUpdates = _playerStates.Values
        });

        foreach (GamePlayer player in _players) player.Send(message, PacketType.ReliableGameData);
    }
    
    public void UpdateGameroomState(RoomState state)
    {
        Logger.LogInfo<GameSimulation>($"Setting GameRoomState to {state}");
        GameroomState.State = state;
        UpdateSystemSyncObject(NetObjectType.ModnationGameroomState, NetworkWriter.Serialize(GameroomState));
    }
    
    private void UpdateSystemSyncObject(int guid, ArraySegment<byte> data)
    {
        if (!_syncObjects.TryGetValue(guid, out SyncObject? syncObject)) return;

        syncObject.Data = data;
        var message = new NetMessageSyncObjectCreate
        {
            MessageType = NetObjectMessageType.Update,
            Guid = syncObject.Guid,
            Data = syncObject.Data
        };

        BroadcastMessage(message, PacketType.ReliableGameData);
    }

    private void UpdateAi()
    {
        if (RaceSettings == null) return;
        var owner = _players.Find(x => x.UserId == _owner);
        string username = owner?.Username ?? string.Empty;
        int numAi = Math.Min(AiInfo.MaxDataSize, RaceSettings.MaxPlayers - _players.Count);
        AiInfo = RaceSettings.AiEnabled ? new AiInfo(username, numAi) : new AiInfo(username, 0);
        UpdateSystemSyncObject(NetObjectType.AiInfo, NetworkWriter.Serialize(AiInfo));
    }

    private void UpdateStartingGrid()
    {
        StartingGrid.Clear();
        foreach (PlayerState state in _playerStates.Values) StartingGrid.Add((int)state.NameUid);
        for (int i = 0; i < AiInfo.Count; ++i)
        {
            int nameUid = CryptoHelper.StringHash32(AiInfo.DataSet[i].UidName);
            StartingGrid.Add(nameUid);
        }

        UpdateSystemSyncObject(NetObjectType.ModnationStartingGrid, NetworkWriter.Serialize(StartingGrid));
    }

    public void OnNetworkMessage(GamePlayer player, NetMessageType type, ArraySegment<byte> data)
    {
        if (type != NetMessageType.MessageUnreliableBlock && type != NetMessageType.VoipPacket)
        {
            // Logger.LogDebug<GameSimulation>($"Received NetMessage {type} from {player.Username} ({(uint)player.UserId}:{(uint)player.PlayerId})");   
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
                UpdateGameroomState(RoomState.Ready);
                break;
            }
            case NetMessageType.GameroomStopTimer:
            {
                if (player.UserId != _owner) break;
                UpdateGameroomState(RoomState.CountingDownPaused);
                break;
            }
            case NetMessageType.SpectatorInfo:
            {
                SpectatorInfo = NetworkReader.Deserialize<SpectatorInfo>(data);
                UpdateSystemSyncObject(NetObjectType.SpectatorInfo, NetworkWriter.Serialize(SpectatorInfo));
                break;
            }
            case NetMessageType.GameroomDownloadTracksComplete:
            {
                if (player.UserId != _owner) break;
                
                GameroomState.LoadEventTime = TimeHelper.LocalTime + BombdConfig.Instance.GameroomCountdownTime;
                GameroomState.LockedForRacerJoinsValue = BombdConfig.Instance.GameroomRacerLockTime;
                GameroomState.LockedTimerValue = BombdConfig.Instance.GameroomTimerLockTime;
                
                UpdateGameroomState(RoomState.CountingDown);
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
                UpdateGameroomState(RoomState.DownloadingTracks);
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

                byte[] array = new byte[data.Count];
                data.CopyTo(array);

                RaceSettings = NetworkReader.Deserialize<EventSettings>(data);
                UpdateSystemSyncObject(NetObjectType.RaceSettings, array);

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
                using NetworkReaderPooled reader = NetworkReaderPool.Get(data);
                int guid = reader.ReadInt32();
                byte[] objectData = reader.ReadBytes(reader.ReadInt32());
                if (_syncObjects.TryGetValue(guid, out SyncObject? syncedObject))
                {
                    // Don't let players update objects they don't own.
                    if (syncedObject.OwnerUserId != player.UserId)
                    {
                        Logger.LogInfo<GameSimulation>(
                            $"Denying update request for SyncObject({syncedObject.Guid}) from {player.Username} since they don't own it.");
                        break;
                    }
                    
                    Logger.LogInfo<GameSimulation>($"Updating SyncObject with Guid = {syncedObject.Guid}");
                    syncedObject.Data = new ArraySegment<byte>(objectData);
                    BroadcastGenericMessage(player, data, NetMessageType.SyncObjectUpdate, PacketType.ReliableGameData);
                }

                break;
            }

            case NetMessageType.SyncObjectCreate:
            {
                if (IsModnation)
                {
                    var message = NetworkReader.Deserialize<NetMessageSyncObjectCreate>(data);
                    if (message.MessageType == NetObjectMessageType.Create)
                    {
                        var syncedObject = new SyncObject
                        {
                            Guid = message.Guid,
                            DebugTag = message.DebugTag,
                            OwnerName = message.OwnerName,
                            OwnerUserId = player.UserId,
                            Type = message.ObjectType
                        };

                        Logger.LogInfo<GameSimulation>(
                            $"Creating SyncObject with Guid = {syncedObject.Guid}, DebugTag = {syncedObject.DebugTag}, OwnerName = {syncedObject.OwnerName}");

                        _syncObjects[syncedObject.Guid] = syncedObject;
                    }
                    else if (message.MessageType == NetObjectMessageType.Update)
                    {
                        if (_syncObjects.TryGetValue(message.Guid, out SyncObject? syncedObject))
                        {
                            // Don't let players update objects they don't own.
                            if (syncedObject.OwnerUserId != player.UserId)
                            {
                                Logger.LogInfo<GameSimulation>(
                                    $"Denying update request for SyncObject({syncedObject.Guid}) from {player.Username} since they don't own it.");
                                break;
                            }

                            Logger.LogInfo<GameSimulation>($"Updating SyncObject with Guid = {syncedObject.Guid}");
                            byte[] array = new byte[message.Data.Count];
                            message.Data.CopyTo(array);
                            syncedObject.Data = new ArraySegment<byte>(array);
                        }
                    }

                    // Send the sync object event to everybody else in the server
                    // TODO: Make sure it's valid
                    BroadcastMessage(player, message, PacketType.ReliableGameData);
                }
                else if (IsKarting)
                {
                    NetworkReaderPooled reader = NetworkReaderPool.Get(data);
                    var syncObject = new SyncObject
                    {
                        OwnerUserId = player.UserId,
                        OwnerName = reader.ReadString(),
                        DebugTag = reader.ReadString(),
                        Type = reader.ReadInt32(),
                        Guid = reader.ReadInt32()
                    };

                    Logger.LogInfo<GameSimulation>(
                        $"Creating SyncObject with Guid = {syncObject.Guid}, DebugTag = {syncObject.DebugTag}, OwnerName = {syncObject.OwnerName}");
                    _syncObjects[syncObject.Guid] = syncObject;
                    BroadcastGenericMessage(player, data, NetMessageType.SyncObjectCreate, PacketType.ReliableGameData);
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
        // TODO: Exclude players who joined after the countdown
        switch (GameroomState.State)
        {
            case RoomState.CountingDown:
            {
                // Wait until the timer has finished counting down, then broadcast to everyone that the race is in progress
                if (TimeHelper.LocalTime >= GameroomState.LoadEventTime)
                {
                    GameroomState.LoadEventTime = 0;
                    GameroomState.LockedTimerValue = 0.0f;
                    GameroomState.LockedForRacerJoinsValue = 0.0f;
                    UpdateGameroomState(RoomState.RaceInProgress);
                    WaitingForPlayerNisEvents = true;
                }

                break;
            }
            case RoomState.RaceInProgress:
            {
                if (WaitingForPlayerNisEvents && _playerStates.Values.All(x => x.ReadyForNis))
                {
                    WaitingForPlayerNisEvents = false;
                    BroadcastGenericIntMessage(TimeHelper.LocalTime, NetMessageType.NisStart, PacketType.ReliableGameData);
                    WaitingForPlayerStartEvents = true;
                }
                
                if (WaitingForPlayerStartEvents && _playerStates.Values.All(x => x.ReadyForEvent))
                {
                    int countdown = TimeHelper.LocalTime + BombdConfig.Instance.EventCountdownTime;
                    BroadcastGenericIntMessage(countdown, NetMessageType.EventStart, PacketType.ReliableGameData);
                    WaitingForPlayerStartEvents = false;
                }

                if (SpectatorInfo.RaceState == RaceState.WaitingForRaceEnd && 
                    TimeHelper.LocalTime >= SpectatorInfo.RaceEndServerTime && !SentEventResults)
                {
                    SentEventResults = true;
                    var xml = Encoding.ASCII.GetBytes(EventResult.Serialize(EventResults));
                    BroadcastGenericMessage(xml, NetMessageType.EventResultsFinal, PacketType.ReliableGameData);
                }
                
                if (SpectatorInfo.RaceState == RaceState.PostRace &&
                    TimeHelper.LocalTime >= SpectatorInfo.PostRaceServerTime)
                {
                    SentEventResults = false;
                    UpdateGameroomState(RoomState.Ready);
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