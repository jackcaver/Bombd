using Bombd.Helpers;
using Bombd.Types.GameBrowser;

namespace Bombd.Types.Requests;

public struct CreateGameRequest
{
    public GameAttributes Attributes;
    public Platform Platform;
    public int OwnerUserId;
}