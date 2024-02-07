using Bombd.Helpers;
using Bombd.Serialization.Wrappers;
using Bombd.Types.GameBrowser;

namespace Bombd.Types.Requests;

public struct GameMigrationRequest
{
    public int HostUserId;
    public Platform Platform;
    public string? GameName;
    public string? Guest;
    public List<GenericInt32> PlayerIdList;
    public GameAttributes? Attributes;
}