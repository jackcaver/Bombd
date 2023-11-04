namespace Bombd.Types.Services;

public class ClientInfo
{
    public int ConnectionId { get; init; }
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
}