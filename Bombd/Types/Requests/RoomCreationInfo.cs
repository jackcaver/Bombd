using Bombd.Helpers;
using Bombd.Simulation;
using Bombd.Types.GameManager;

namespace Bombd.Types.Requests;

public struct RoomCreationInfo
{
    public GameManagerGame Game;
    public ServerType Type;
    public Platform Platform;
    public int MaxPlayers;
    public int OwnerUserId;
}