namespace Bombd.Types.Network;

// NetMessage Versions
// 0x110  ModNation Racers Public Beta              Count = 0x2d, Checksum = 94029
// 0x115  ModNation Racers                          Count = 0x34, Checksum = 186793
// 0x229  LittleBigPlanet Karting Milestone 4       Count = 0x40, Checksum = 38697
// 0x230  LittleBigPlanet Karting Public Beta       Count = 0x45, Checksum = 87605
// 0x233  LittleBigPlanet Karting                   Count = 0x48, Checksum = 87659


// Messages that can be received by client in ModNation Racers
// 0x0a  ItemHitConfirm
// 0x0c  SecondaryCollision
// 0x0d  WandererUpdate
// 0x0e  WorldObjectStateChange
// 0x10  ItemCreate
// 0x11  ItemDestroy
// 0x12  ItemMoveData (Not sure what the condition for sending/receiving this is)
// 0x13  ItemHitPlayer
// 0x15  EventStart
// 0x17  EventResultsFinal
// 0x18  GenericGameplay
// 0x1a  ArbitratedItemCreate
// 0x1b  ArbiratedItemDestroy
// 0x1c  ArbitratedItemAcquire
// 0x1d  ArbitratedItemRelease
// 0x20  TextChatMsg
// 0x21  SeriesResults
// 0x2a  ArbitratedItemAcquireFailed
// 0x2b  MessageUnreliableBlock[0x2 KartNetState]
// 0x2c  PlayerSessionInfo
// 0x2d  SyncObjectCreate
// 0x2f  PlayerFinishedEvent
// 0x31  InviteChallengeMessageModnation
// 0x32  InviteSessionJoinDataModNation
// 0x34  BulkPlayerStateUpdate
// 0x35  RandomSeed
// 0x3a  NisStart
// 0x3b  ArbitratedItemCreateBlock
// 0x3e  InviteRequestJoin
// 0x3f  PlayerLeave

// Messages that can be received by the client in LittleBigPlanet Karting
// 0x0a  ItemHitConfirm
// 0x0b  TeamMessage
// 0x0c  SecondaryCollision
// 0x10  ItemCreate
// 0x11  ItemDestroy
// 0x12  ItemMoveData (Not sure what the condition for sending/receiving this is)
// 0x13  ItemHitPlayer
// 0x15  EventStart
// 0x17  EventResultsFinal
// 0x18  GenericGameplay
// 0x1a  ArbitratedItemCreate
// 0x1b  ArbitratedItemDestroy
// 0x1c  ArbitratedItemAcquire
// 0x1d  ArbitratedItemRelease
// 0x1e  ArbitratedItemDestroyBlock
// 0x20  TextChatMsg
// 0x21  SeriesResults
// 0x2a  ArbitratedItemAcquireFailed
// 0x2b  MessageUnreliableBlock[0x1 PodNetState, 0x2 KartNetState, 0x51 ControllerNetState]
// 0x2c  PlayerSessionInfo
// 0x2d  SyncObjectCreate
// 0x2f  PlayerFinishedEvent
// 0x33  VoipPacket
// 0x34  BulkPlayerStateUpdate
// 0x35  RandomSeed
// 0x3a  NisStart
// 0x3b  ArbitratedItemCreateBlock
// 0x3f  PlayerLeave
// 0x40  SyncObjectUpdate
// 0x41  SyncObjectRemove
// 0x42  EventRestart
// 0x43  PlayerCreateInfo
// 0x48  GroupLeaderMatchmakingStatus
// 0x49  PlayerDetachGuestInfo
// 0x4a  MessageReliableBlock[0xe WorldObjectStateChange, 0x50 WorldObjectTime]
// 0x4c  ArbitratedItemAcquireBlock
// 0x4e  WorldObjectCreate
// 0x4f  CoopPostraceLeaderAction


// Messages that can be sent by client in ModNation Racers
// 0x0a  ItemHitConfirm
// 0x0c  SecondaryCollision
// 0x0d  WandererUpdate
// 0x0e  WorldObjectStateChange
// 0x11  ItemDestroy
// 0x13  ItemHitPlayer
// 0x14  ReadyForEventStart
// 0x16  EventResultsPreliminary
// ??? 0x18
// 0x19  PlayerStateUpdate
// 0x1a  ArbitratedItemCreate
// 0x1b  ArbitratedItemDestroy
// 0x1c  ArbitratedItemAcquire
// ??? 0X1D
// 0x1e  ArbitratedItemDestroyBlock
// 0x1f  EventSettingsUpdate
// 0x20  TextChatMsg
// 0x25  GameroomReady
// 0x27  LeaderChangeRequest
// 0x28  KickPlayerRequest
// 0x29  RankedEventVeto
// 0x2b  MessageUnreliableBlock[0x2 KartNetState]
// 0x2d  SyncObjectCreate
// 0x2e  GameroomStopTimer
// 0x2f  PlayerFinishedEvent
// 0x30  SpectatorInfo
// 0x31  InviteChallengeMessageModnation
// 0x32  InviteSessionJoinDataModnation
// 0x33  VoipPacket
// 0x37  GameroomDownloadTracksComplete
// 0x38  GameroomDownloadTracksFailed
// 0x39 ReadyForNisStart
// 0x3b  ArbitratedItemCreateBlock
// 0x3c  GameroomRequestStartEvent
// 0x3d  GameroomLeaderVeto
// 0x3e  InviteRequestJoin
// 0x3f  PlayerLeave

public enum NetMessageType : byte
{
    None = 0,
    PodNetState = 1, // size = 40
    KartNetState = 2, // size = 48
    // 0x3
    // 0x4
    // 0x5
    // 0x6
    // 0x7
    // 0x8
    // 0x9
    ItemHitConfirm = 0xa, // size = 132
    TeamMessage = 0xb, // size = 12
    SecondaryCollision = 0xc, // size = 132
    WandererUpdate = 0xd, // size = 132
    WorldObjectStateChange = 0xe, // size = 508
    ItemCreate = 0x10, // size = 132
    ItemDestroy = 0x11, // size = 132
    ItemMoveData = 0x12, // size = 508
    ItemHitPlayer = 0x13, // size = 132
    ReadyForEventStart = 0x14, // size = 4
    EventStart = 0x15, // size = 4
    EventResultsPreliminary = 0x16, // size = 20
    EventResultsFinal = 0x17, // size = 20
    
    // 0xd = KartTransformerData? (0xfbc in Karting)
    GenericGameplay = 0x18, // size = 12
    PlayerStateUpdate = 0x19, // size = 8
    
    // mItemType
    // mItemId
    // mPlayerNameUID
    // mTimeout
    // mParam
    // mFlags
    ArbitratedItemCreate = 0x1a, // size = 24
    ArbitratedItemDestroy = 0x1b, // size = 24
    
    // KARTING
        // int mItemType
        // int mItemId
        // int mPlayerNameUID
        // int mTimeout
        // int mParam
        // int mFlags
    // MODNATION
        // int ??? some kind of index?
        // int mItemId
        // int mPlayerNameUID
        // int mTimeout - Unused?
        // int ??? - No idea, flags or param maybe
    ArbitratedItemAcquire = 0x1c, // size = 24
    
    // KARTING
        // int mItemType
        // int mItemId
        // int mPlayerNameUID
        // int mTimeout
        // int mParam
        // int mFlags
    ArbitratedItemRelease = 0x1d, // size = 24
    
    ArbitratedItemDestroyBlock = 0x1e, // size = 12304
    EventSettingsUpdate = 0x1f, // size = 132
    TextChatMsg = 0x20, // size = 8
    SeriesResults = 0x21, // size = 20
    // 0x22
    // 0x23
    // 0x24
    GameroomReady = 0x25, // size = 4
    // 0x26
    LeaderChangeRequest = 0x27, // size = 8
    KickPlayerRequest = 0x28, // size = 8
    RankedEventVeto = 0x29, // size = 4
    ArbitratedItemAcquireFailed = 0x2a, // size = 24
    MessageUnreliableBlock = 0x2b, // size = 20
    PlayerSessionInfo = 0x2c, // size = 3844
    SyncObjectCreate = 0x2d, // size = 72
    GameroomStopTimer = 0x2e, // size = 4
    PlayerFinishedEvent = 0x2f, // size = 4
    SpectatorInfo = 0x30, // size = 52
    InviteChallengeMessageModnation = 0x31, // ChallengeInvite
    InviteSessionJoinDataModnation = 0x32, // ChallengeInvite
    VoipPacket = 0x33, // size = 1028
    BulkPlayerStateUpdate = 0x34, // size = 772
    RandomSeed = 0x35, // size = 4
    // 0x36
    GameroomDownloadTracksComplete = 0x37, // size = 4
    GameroomDownloadTracksFailed = 0x38, // size = 4
    ReadyForNisStart = 0x39, // size = 4
    NisStart = 0x3a, // size = 4
    
    // MODNATION
        // int mItemBlockCount
        // struct[mItemBlockCount] Blocks
            // int mPlayerNameUID
            // int mItemId
    // KARTING
        // int mItemBlockCount
        // int mItemType
        // int mPlayerNameUID
        // int mAcquireBehaviour
        // int[mItemBlockCount] mItemIds
    ArbitratedItemCreateBlock = 0x3b, // size = 12304
    
    GameroomRequestStartEvent = 0x3c, // size = 4
    GameroomLeaderVeto = 0x3d, // size = 4
    InviteRequestJoin = 0x3e, // size = 68, ChallengeCancel
    PlayerLeave = 0x3f, // size = 36
    SyncObjectUpdate = 0x40, // size = 12
    SyncObjectRemove = 0x41, // size = 12
    EventRestart = 0x42, // size = 4
    PlayerCreateInfo = 0x43, // size = 3844
    SeriesSettingsUpdate = 0x44, // size = 1364
    InviteInviteMessage = 0x45, // size = 68
    InviteChallengeMessage = 0x46, // size = 68
    InviteSessionJoinData = 0x47, // size = 524
    GroupLeaderMatchmakingStatus = 0x48, // size = 4
    
    // ItemMoveData = 0x49, size = 508, has the same type as PlayerDetachGuestInfo for some reason, also duplicated?
    PlayerDetachGuestInfo = 0x49, // size: 3844
    MessageReliableBlock = 0x4a, // size: 20
    TeamSettingsUpdate = 0x4b, // size: 16
    ArbitratedItemAcquireBlock = 0x4c, // size: 36872
    
    // int
    PostRaceVoteForTrack = 0x4d, // size: 4
    
    // mUidCount
    // mMsgType
    // mEmitterUid
        // mUids
        //
    WorldObjectCreate = 0x4e, // size: 4012
    
    // mPlaygroupId
    // mOption
    CoopPostraceLeaderAction = 0x4f, // size: 8
    WorldObjectTime = 0x50, // size: 4
    ControllerNetState = 0x51 // size: 12
}