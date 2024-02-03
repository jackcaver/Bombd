using Bombd.Types.Network.Simulation;

namespace Bombd.Types.Events;

public struct PlayerLeaveEventArgs
{
    public GameRoom Room;
    public string PlayerName;
    public string Reason;
}