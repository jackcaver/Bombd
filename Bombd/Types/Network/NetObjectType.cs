using Bombd.Helpers;

namespace Bombd.Types.Network;

public static class NetObjectType
{
    public static readonly NetObjectTypeInfo Unknown = new("NET_OBJECT_TYPE_UNKNOWN", 0);
    public static readonly NetObjectTypeInfo PlayerConfig = new("NET_OBJECT_PLAYERCONFIG", 1);
    public static readonly NetObjectTypeInfo RaceSettings = new("NET_OBJECT_RACE_SETTINGS", 2);
    public static readonly NetObjectTypeInfo SpectatorInfo = new("NET_OBJECT_SPECTATOR_INFO", 3);
    public static readonly NetObjectTypeInfo AiInfo = new("NET_OBJECT_AI_INFO", 4);
    public static readonly NetObjectTypeInfo NetCoiInfoPackage = new("NET_OBJECT_COI_INFO", 5);
    public static readonly NetObjectTypeInfo VotePackage = new("NET_OBJECT_VOTE_PACKAGE", 5);
    public static readonly NetObjectTypeInfo SeriesInfo = new("NET_OBJECT_SERIES_INFO", 6);
    public static readonly NetObjectTypeInfo GameroomState = new("NET_OBJECT_GAMEROOM_STATE", 7);
    public static readonly NetObjectTypeInfo StartingGrid = new("NET_OBJECT_STARTING_GRID", 8);
    public static readonly NetObjectTypeInfo BigBlob = new("NET_OBJECT_BIG_BLOB", 9);
    public static readonly NetObjectTypeInfo PodConfig = new("NET_OBJECT_POD_CONFIG", 10);
    public static readonly NetObjectTypeInfo PlayerAvatar = new("NET_OBJECT_PLAYER_AVATAR", 11);
}