using Bombd.Helpers;
using Bombd.Types.GameBrowser;

namespace Bombd.Types.Network.Requests;

public struct CreateGameRequest
{
    public GameAttributes Attributes;
    public Platform Platform;
    public int OwnerUserId;
}