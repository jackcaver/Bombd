namespace Bombd.Types.Services;

public class Session
{
    public string GameName = string.Empty;
    
    public string PlayerConnectUuid = string.Empty;
    public int PlayerConnectId;
    public string Username = string.Empty;
    public int Issuer;
    
    public uint HashSalt;
}