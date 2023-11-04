using Bombd.Helpers;
using Bombd.Types.GameBrowser;

namespace Bombd.Types.Network.Requests;

public struct GameMigrationRequest
{
    public int HostUserId;
    public Platform Platform;
    public string? GameName;
    public List<GenericInt32> PlayerIdList;
    public GameAttributes? Attributes;
}