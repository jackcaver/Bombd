using Bombd.Helpers;
using Bombd.Types.Network;

namespace Bombd.Types.Services;

public class Session
{
    public HashSet<string> ConnectedServices = new();
    public int HashSalt;
    public string GameName;
}