using Bombd.Types.GameBrowser;
using Bombd.Types.Network.Simulation;

namespace Bombd.Types.Events;

public struct GameEventArgs(GameEventType type, GameRoom room)
{
    public readonly GameEventType Type = type;
    public readonly GameRoom Room = room;
}