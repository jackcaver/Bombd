using Bombd.Helpers;
using Bombd.Types.GameManager;
using Bombd.Types.Network.Simulation;

namespace Bombd.Types.Requests;

public struct RoomCreationInfo
{
    public GameManagerGame Game;
    public ServerType Type;
    public Platform Platform;
    public int MaxPlayers;
    public int OwnerUserId;
    public bool IsRanked;
    public bool IsSeries;
}