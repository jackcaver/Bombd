namespace Bombd.Protocols;

public enum ConnectionState
{
    Disconnected,
    WaitingForHandshake,
    WaitingForConnection,
    WaitingForTimeSync,
    Connected
}