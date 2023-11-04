namespace Bombd.Types.Services;

public class Session
{
    public HashSet<string> ConnectedServices = new();
    public string GameName;
    public int HashSalt;
}