using Bombd.Types.Network;

namespace Bombd.Types.Events;

public struct PlayerJoinEventArgs
{
    public GameRoom Room;
    public GamePlayer Player;
    public bool WasMigration;
}