using Bombd.Serialization;
using Bombd.Types.Network;

namespace Bombd.Simulation;

public class GameGuest
{
    public GameGuest(string playerName, string guestName)
    {
        PlayerName = playerName;
        GuestName = guestName;
    }
    
    public readonly string PlayerName;
    public readonly string GuestName;
    public PlayerInfo? Info;
    public int NameUid;
}