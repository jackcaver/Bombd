namespace Bombd.Types.Network;

// Create = 0
// Acquire = 1
// Release = 2
// Destroy = 3
// AcquireFail = 4



// fn (event_type, item_id, + 0x18, player_name_uid, + 0x8) 

public enum NetMessageType : byte
{
    KartNetState = 2,
    ItemHitConfirm = 0xa,
    TeamMessage = 0xb,
    SecondaryCollision = 0xc,
    WandererUpdate = 0xd,
    Gameplay = 0xe,
    ItemMessage_0x10 = 0x10,
    ItemDestroy = 0x11,
    ItemMoveData = 0x12,
    ItemHitPlayer = 0x13,
    ReadyForEventStart = 0x14,
    EventStart = 0x15,
    EventResultsPreliminary = 0x16,
    EventResultsFinal = 0x17,
    GenericGameplay = 0x18,
    PlayerStateUpdate = 0x19,
    
    // mItemType
    // mItemId
    // mPlayerNameUID
    // mTimeout
    // mParam
    // mFlags
    ArbitratedItemCreate = 0x1a,
    
    ArbitratedItemDestroy = 0x1b,
    
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
    ArbitratedItemAcquire = 0x1c,
    
    // KARTING
        // int mItemType
        // int mItemId
        // int mPlayerNameUID
        // int mTimeout
        // int mParam
        // int mFlags
    ArbitratedItemRelease = 0x1d,
    
    ArbitratedItemDestroyBlock = 0x1e,
    EventSettingsUpdate = 0x1f,
    TextChatMsg = 0x20,
    SeriesResults = 0x21,
    GameroomReady = 0x25,
    LeaderChangeRequest = 0x27,
    KickPlayerRequest = 0x28,
    RankedEventVeto = 0x29,
    ArbitratedItemAcquireFailed = 0x2a,
    MessageUnreliableBlock = 0x2b,
    PlayerSessionInfo = 0x2c,
    SyncObjectCreate = 0x2d,
    GameroomStopTimer = 0x2e,
    PlayerFinishedEvent = 0x2f,
    SpectatorInfo = 0x30,
    InviteChallengeMessageModnation = 0x31,
    InviteSessionJoinDataModnation = 0x32,
    VoipPacket = 0x33,
    BulkPlayerStateUpdate = 0x34,
    RandomSeed = 0x35,
    GameroomDownloadTracksComplete = 0x37,
    GameroomDownloadTracksFailed = 0x38,
    ReadyForNisStart = 0x39,
    NisStart = 0x3a,
    
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
    ArbitratedItemCreateBlock = 0x3b,
    
    GameroomRequestStartEvent = 0x3c,
    GameroomLeaderVeto = 0x3d,
    InviteRequestJoin = 0x3e,
    PlayerLeave = 0x3f,
    SyncObjectUpdate = 0x40,
    SyncObjectRemove = 0x41,
    EventRestart = 0x42,
    PlayerCreateInfo = 0x43,
    SeriesSettingsUpdate = 0x44,
    InviteInviteMessage = 0x45,
    InviteChallengeMessage = 0x46,
    InviteSessionJoinData = 0x47,
    GroupLeaderMatchmakingStatus = 0x48,
    PlayerDetachGuestInfo = 0x49,
    MessageReliableBlock = 0x4a,
    TeamSettingsUpdate = 0x4b,
    ArbitratedItemAcquireBlock = 0x4c,
    
    // int
    PostRaceVoteForTrack = 0x4d,
    
    // mUidCount
    // mMsgType
    // mEmitterUid
        // mUids
        //
    WorldobjectCreate = 0x4e,
    
    // mPlaygroupId
    // mOption
    CoopPostraceLeaderAction = 0x4f
}