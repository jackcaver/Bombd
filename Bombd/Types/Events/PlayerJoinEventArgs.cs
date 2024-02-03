using Bombd.Types.Network.Simulation;

namespace Bombd.Types.Events;

public struct PlayerJoinEventArgs
{
    public GameRoom Room;
    public GamePlayer Player;
    public bool WasMigration;
}