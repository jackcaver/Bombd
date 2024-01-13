using Bombd.Simulation;
using Bombd.Types.Network;

namespace Bombd.Types.Events;

public struct GameEventArgs
{
    public GameEventType EventType;
    public GameRoom Room;
}