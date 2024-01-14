using Bombd.Core;
using Bombd.Globals;
using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Types.Network;
using Bombd.Types.Network.Messages;
using Bombd.Types.Network.Objects;
using Bombd.Types.Network.Races;
using Bombd.Types.Network.Room;

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
    public readonly bool IsRanked;
    public bool EventStarted { get; private set; }
    public bool IsKarting => Platform == Platform.Karting;
    public bool IsModNation => Platform == Platform.ModNation;
    public bool HasRaceSettings => _raceSettings != null;
    public bool HasSeriesSetting => _seriesInfo != null;

    private RaceConstants _raceConstants;

    private GenericSyncObject<CoiInfo> _coiInfo;
    private GenericSyncObject<VotePackage> _votePackage;
    private GenericSyncObject<GameroomState> _gameroomState;
    private GenericSyncObject<AiInfo> _aiInfo;
    private GenericSyncObject<SpectatorInfo> _raceInfo;
    private GenericSyncObject<StartingGrid> _startingGrid;
    private GenericSyncObject<EventSettings>? _raceSettings;
    private GenericSyncObject<SeriesInfo>? _seriesInfo;
    private List<EventResult> _eventResults = new();

    private bool _waitingForPlayerNisEvents;
    private bool _waitingForPlayerStartEvents;
    private bool _hasSentEventResults;
    private float _pausedTimeRemaining;

    public GameSimulation(ServerType type, GameRoom room, int owner, bool isRanked, bool isSeries)
    {
        Room = room;
        Type = type;
        Platform = room.Platform;
        Owner = owner;
        _players = room.Game.Players;
        IsRanked = isRanked;
        
        if (Platform == Platform.ModNation)
            _raceConstants = isRanked ? RaceConstants.Ranked : RaceConstants.ModNation;
        else _raceConstants = RaceConstants.Karting;
        
        Logger.LogInfo<GameSimulation>($"Starting Game Simulation ({Type}:{Platform}, IsRanked = {isRanked}, IsSeries = {isSeries})");
        
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

            if (!isRanked) return;

            EventSettings raceSettings;
            if (isSeries)
            {
                var series = Career.ModNation.GetRankedSeries(5, Owner);
                _seriesInfo = CreateSystemSyncObject(series, NetObjectType.SeriesInfo);
                raceSettings = series.Events.First();
            }
            else
            {
                raceSettings = Career.ModNation.GetRankedEvent(Owner);
            }
            
            _raceSettings = CreateSystemSyncObject(raceSettings, NetObjectType.RaceSettings);
            Room.UpdateAttributes(raceSettings);
        }
    }

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
        
        // Send a leave reason to everybody else in the session
        BroadcastMessage(new NetMessagePlayerLeave(Platform)
        {
            PlayerName = player.Username,
            Reason = player.LeaveReason
        }, PacketType.ReliableGameData);
        
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

            if (_seriesInfo != null)
            {
                foreach (var evt in _seriesInfo.Value.Events)
                    evt.OwnerNetcodeUserId = Owner;
                _seriesInfo.Sync();
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

    private void BroadcastMessage(INetworkMessage message, NetMessageType messageType, PacketType type)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> payload = NetworkMessages.Pack(writer, message);
        payload[0] = (byte)messageType;
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
            _pausedTimeRemaining = room.LoadEventTime - TimeHelper.LocalTime;
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
                player.SetupNextRaceState();
                player.State.Flags &= ~PlayerStateFlags.DownloadedTracks;
            }
        }
        
        switch (state)
        {
            case RoomState.None:
            { 
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
            _startingGrid.Value.Add(new GridPositionData(player.State.NameUid, false));
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
                _aiInfo.Value.DataSet[i].NameUid,
                false
            ));
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
                    guest.NameUid = CryptoHelper.StringHashU32(config.UidName);
            }
            else if (config.Type == 0)
            {
                uint nameUid = CryptoHelper.StringHashU32(config.UidName);
                if (config.NetcodeUserId != player.UserId || nameUid != player.State.NameUid)
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
    
    public void OnNetworkMessage(GamePlayer player, uint senderNameUid, NetMessageType type, ArraySegment<byte> data)
    {
        if (
            type != NetMessageType.VoipPacket && 
            type != NetMessageType.SpectatorInfo &&
            type != NetMessageType.MessageUnreliableBlock && 
            type != NetMessageType.MessageReliableBlock &&
            type != NetMessageType.WandererUpdate &&
            type != NetMessageType.GenericGameplay &&
            type != NetMessageType.Gameplay)
        {
            Logger.LogTrace<GameSimulation>($"Received NetMessage {type} from {player.Username} ({(uint)player.UserId}:{player.State.NameUid}:{(uint)player.PlayerId})");   
        }

        // The name UID should match the one we've received from the player state update
        if (player.State.HasNameUid && senderNameUid != player.State.NameUid)
        {
            Logger.LogWarning<GameSimulation>($"NameUID for {player.Username} doesn't match as expected! Disconnecting! ({player.State.NameUid} != {senderNameUid}");
            player.Disconnect();
            return;
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
            case NetMessageType.ArbitratedItemDestroy:
            case NetMessageType.ArbitratedItemCreate:
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
                    guest.NameUid = CryptoHelper.StringHashU32(guestInfo.NameUid);
                    
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
                
                // We'll send the leave message when the player actually gets disconnected from the session
                player.LeaveReason = message.Reason;
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
            case NetMessageType.RankedEventVeto:
            {
                if (!IsRanked || player.State.HasEventVetoed) return;
                
                player.State.HasEventVetoed = true;
                if (_players.All(p => p.State.HasEventVetoed))
                {
                    _gameroomState.Value.HasEventVetoOccured = true;
                    _gameroomState.Sync();
                    
                    EventSettings raceSettings;
                    if (_seriesInfo != null)
                    {
                        var series = Career.ModNation.GetRankedSeries(5, Owner);
                        _seriesInfo.Value = series;
                        raceSettings = series.Events.First();
                    }
                    else
                    {
                        raceSettings = Career.ModNation.GetRankedEvent(Owner, _raceSettings.Value.CreationId);
                    }

                    raceSettings.UpdateReason = EventUpdateReason.RaceSettingsVetoed;
                    _raceSettings.Value = raceSettings;
                    Room.UpdateAttributes(raceSettings);
                }

                BroadcastGenericIntMessage((int)player.State.NameUid, NetMessageType.RankedEventVeto, PacketType.ReliableGameData);
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

                    if (oldState != newState)
                    {
                        Logger.LogDebug<GameSimulation>("RaceState: " + _raceInfo.Value.RaceState);
                        Logger.LogDebug<GameSimulation>("RaceEndServerTime: " + _raceInfo.Value.RaceEndServerTime);
                        Logger.LogDebug<GameSimulation>("PostRaceServerTime: " + _raceInfo.Value.PostRaceServerTime);

                        // The current race has been completed
                        if (newState == RaceState.PostRace)
                        {
                            // Reset the flags for the next race
                            foreach (var racer in _players) racer.SetupNextRaceState();
                            EventStarted = false;
                            _waitingForPlayerNisEvents = true; 
                            _waitingForPlayerStartEvents = false; 
                            _hasSentEventResults = false;
                        }
                        
                        // A single race has ended and we're returning to the gameroom
                        if (newState == RaceState.Invalid)
                        {
                            _waitingForPlayerNisEvents = false;
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
                    }
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
                    
                    string xml = reader.ReadString(len);
                    Logger.LogDebug<GameSimulation>(xml);
                    results = EventResult.Deserialize(xml);
                }
                catch (Exception)
                {
                    Logger.LogWarning<GameSimulation>($"Failed to parse EventResultsPreliminary for {player.Username}, disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }

                bool isValid = true;
                foreach (var result in results)
                {
                    bool isMyAi = _aiInfo.Value.DataSet.Any(ai => ai.NameUid == result.OwnerUid && ai.OwnerName == player.Username);
                    bool isMe = result.OwnerUid == player.State.NameUid;
                    bool isGuest = player.Guests.Any(guest => guest.NameUid == result.OwnerUid);

                    isValid = isMyAi || isMe || isGuest;
                    if (!isValid) break;
                    if (isMe) player.Score = result.EventScore;
                }

                if (!isValid)
                {
                    Logger.LogWarning<GameSimulation>($"{player.Username} tried to submit invalid event results. Disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }

                player.HasSentRaceResults = true;
                _eventResults.AddRange(results);
                break;
            }
            case NetMessageType.TextChatMsg:
            {
                try
                {
                    NetChatMessage message = NetChatMessage.LoadXml(data, Platform);
                    // Basic spoofing prevention, don't allow sender name mismatches
                    if (message.Sender != player.Username)
                    {
                        Logger.LogWarning<GameSimulation>($"TextChatMsg from {player.Username} has mis-matching sender name {message.Sender}, disconnecting them from the session.");
                        player.Disconnect();
                        break;
                    }
                }
                catch
                {
                    Logger.LogWarning<GameSimulation>($"Failed to parse TextChatMsg from {player.Username}, disconnecting them from the session.");
                    player.Disconnect();
                    break;
                }
                
                BroadcastGenericMessage(player, data, type, PacketType.ReliableGameData);
                break;
            }

            case NetMessageType.InviteSessionJoinDataModnation:
            {
                // challenge nameuid
                // challengee nameuid
                // invite status 1
                
                BroadcastGenericMessage(player, data, type, PacketType.ReliableGameData);
                break;
            }

            case NetMessageType.InviteChallengeMessageModnation:
            {
                // challenger nameuid
                // challengee nameuid

                BroadcastGenericMessage(player, data, type, PacketType.ReliableGameData);
                break;
            }

            case NetMessageType.InviteRequestJoin:
            {
                // challenge nameuid
                // challengee nameuid
                // invite status
                BroadcastGenericMessage(player, data, type, PacketType.ReliableGameData);
                break;
            }

            case NetMessageType.PlayerFinishedEvent:
            {
                player.HasFinishedRace = true;
                BroadcastGenericMessage(player, data, type, PacketType.ReliableGameData);
                break;
            }
            
            case NetMessageType.GroupLeaderMatchmakingStatus:
            case NetMessageType.GenericGameplay:
            case NetMessageType.Gameplay:
            case NetMessageType.WandererUpdate:
            {
                BroadcastGenericMessage(player, data, type, PacketType.ReliableGameData);
                break;
            }
            case NetMessageType.EventSettingsUpdate:
            {
                // Only the host should be allowed to update unranked event settings I'm fairly sure
                if (player.UserId != Owner || IsRanked) break;

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

                // We don't know if we're doing a top tracks race until the host sends the
                // initial event settings, switch to a series race here.
                if (settings.CareerEventIndex == CoiInfo.SPHERE_INDEX_TOP_TRACKS)
                {
                    var series = WebApiManager.GetTopTrackSeries(Owner, settings.KartParkHome);
                    _seriesInfo = CreateSystemSyncObject(series, NetObjectType.SeriesInfo);
                    settings = series.Events[0];
                }
                
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

                if (state.NameUid != senderNameUid)
                {
                    Logger.LogWarning<GameSimulation>($"PlayerStateUpdate NameUid doesn't match SenderNameUid! Disconnecting player!");
                    player.Disconnect();
                    break;
                }
                
                // If we're still connecting, then this is the first player state update before we're actually in-game,
                // so check if anybody else has our name UID first, this will only happen if someone is exploiting or if
                // somebody on RPCS3 doesn't have their Console ID set
                if (!player.State.HasNameUid)
                {
                    if (_players.Any(p => p.State.NameUid == state.NameUid))
                    {
                        Logger.LogWarning<GameSimulation>($"Disconnecting {player.Username} from game since another user already has the same UID!");
                        player.Disconnect();
                        break;
                    }
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

                // byte[] b = new byte[data.Count];
                // data.CopyTo(b, 0);
                // File.WriteAllBytes(type.ToString(), b);

                break;
            }
        }
    }

    private string FinalizeEventResults()
    {
        RaceType mode = _raceSettings!.Value.RaceType;
        ScoreboardType scoreboard = _raceSettings!.Value.ScoreboardType;

        if (IsKarting)
        {
            if (mode == RaceType.Battle)
                _eventResults.Sort((a, z) => z.BattleKills.CompareTo(a.BattleKills));
            else if (scoreboard == ScoreboardType.Score)
                _eventResults.Sort((a, z) => z.PointsScored.CompareTo(a.PointsScored));
            else
                _eventResults.Sort((a, z) => a.EventScore.CompareTo(z.EventScore));
        }
        // ModNation always sorts by time score
        else _eventResults.Sort((a, z) => a.EventScore.CompareTo(z.EventScore));

        string xml = EventResult.Serialize(_eventResults);
        _eventResults.Clear();

        return xml;
    }

    private string FinalizeSeriesResults()
    {
        var racers = _players.Where(p => !p.IsSpectator).OrderBy(p => p.Score).ToList();
        for (int i = 0; i < racers.Count; ++i)
        {
            racers[i].Points = RaceConstants.SeriesPoints[i];
            racers[i].TotalPoints += RaceConstants.SeriesPoints[i];
        }

        var results = racers.OrderByDescending(p => p.TotalPoints).Select((p, i) => new SeriesResult()
        {
            OwnerName = p.Username,
            TotalPoints = p.TotalPoints,
            PointsEarned = p.Points,
            DidNotFinish = p.HasFinishedRace ? 0 : 1,
            Ranking = i + 1
        }).ToList();
        
        return SeriesResult.Serialize(results);
    }
    
    private void SimUpdate()
    {
        var room = _gameroomState.Value;
        var raceInfo = _raceInfo.Value;
        
        // If the race hasn't started update the gameroom's state based on 
        // the current players in the lobby.
        if (_raceSettings != null && room.State < RoomState.RaceInProgress)
        {
            int numReadyPlayers =
                _players.Count(x => (x.State.Flags & PlayerStateFlags.GameRoomReady) != 0);
            bool hasMinPlayers = numReadyPlayers >= _raceSettings.Value.MinHumans;

            // Karting, series races, and ranked races don't allow the "owner" to start the race
            bool isAutoStart = IsKarting || IsRanked || _seriesInfo != null;
            
            if (hasMinPlayers && room.State == RoomState.WaitingMinPlayers)
                SetCurrentGameroomState(RoomState.Ready);
            else if (isAutoStart && room.State == RoomState.Ready)
                SetCurrentGameroomState(RoomState.CountingDown);
            else if (!hasMinPlayers)
                SetCurrentGameroomState(RoomState.WaitingMinPlayers);
        }
        
        switch (room.State)
        {
            case RoomState.CountingDown:
            {
                // I think the countdown should only pause for connecting people in ranked races?
                // Since the countdown auto-starts after minimum players is reached?
                if (IsRanked)
                {
                    float timeRemaining = room.LoadEventTime - TimeHelper.LocalTime;
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
                if (!EventStarted)
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
                            int countdown = TimeHelper.LocalTime + _raceConstants.EventCountdownTime;
                            BroadcastGenericIntMessage(countdown, NetMessageType.EventStart, PacketType.ReliableGameData);
                            _waitingForPlayerStartEvents = false;
                            EventStarted = true;
                        }
                    }
                }

                bool shouldSendResults = 
                    (raceInfo.RaceState == RaceState.WaitingForRaceEnd && TimeHelper.LocalTime >= raceInfo.RaceEndServerTime) ||
                    _players.All(player => player.IsSpectator || player.HasSentRaceResults);

                if (shouldSendResults && !_hasSentEventResults && EventStarted)
                {
                    string destination = IsKarting ? "destPod" : "destGameroom";
                    if (_raceSettings != null && _seriesInfo != null)
                    {
                        var events = _seriesInfo.Value.Events;
                        int nextSeriesIndex = _raceSettings.Value.SeriesEventIndex + 1;
                        if (nextSeriesIndex < events.Count)
                        {
                            var nextEvent = events[nextSeriesIndex];
                            _raceSettings.Value = nextEvent;
                            Room.UpdateAttributes(nextEvent);
                            destination = "destNextSeriesRace";
                        } else destination = IsRanked ? "destKartPark" : "destPostSeries";
                    }
                    // Single xp races in ModNation just return back to the kart park
                    else if (IsRanked) destination = "destKartPark";

                    Logger.LogInfo<GameSimulation>($"{Room.Game.GameName} race has been completed, destination is {destination}");

                    int postRaceDelay = _raceConstants.PostRaceTime;
                    var message = new NetMessageEventResults
                    {
                        SenderNameUid = NetworkMessages.SimServerUid,
                        Platform = Platform,
                        ResultsXml = FinalizeEventResults(),
                        Destination = destination,
                        PostEventDelayTime = TimeHelper.LocalTime + postRaceDelay,
                        PostEventScreenTime = postRaceDelay
                    };
                    
                    BroadcastMessage(message, NetMessageType.EventResultsFinal, PacketType.ReliableGameData);
                    if (_seriesInfo != null)
                    {
                        message.ResultsXml = FinalizeSeriesResults();
                        BroadcastMessage(message, NetMessageType.SeriesResults, PacketType.ReliableGameData);
                    }
                    
                    if (IsKarting) UpdateVotePackage();

                    // Broadcast new seed for the next race
                    _seed = CryptoHelper.GetRandomSecret();
                    BroadcastMessage(new NetMessageRandomSeed { Seed = _seed }, PacketType.ReliableGameData);
                    _hasSentEventResults = true;
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