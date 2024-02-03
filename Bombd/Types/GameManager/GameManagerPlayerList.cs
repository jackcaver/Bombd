using Bombd.Helpers;
using Bombd.Serialization;
using Bombd.Types.Network.Simulation;

namespace Bombd.Types.GameManager;

public class GameManagerPlayerList : List<GamePlayer>, INetworkWritable
{
    private const int MaxPlayers = 64;

    public GameManagerPlayerList() : base(MaxPlayers)
    {
    }

    public GameManagerPlayerList(IEnumerable<GamePlayer> players) : base(players)
    {
    }

    public void Write(NetworkWriter writer)
    {
        foreach (GamePlayer player in this)
        {
            writer.Write(player.PlayerId);
            writer.Write(player.UserId);
            writer.Write(player.Username, 0x24);
            writer.Write(player.Guests.Count);
            
            // Maybe include a version component somewhere in the NetworkWriter, but for
            // now this hack is fine.
            if (player.Platform == Platform.Karting) writer.Clear(0x4);
            else writer.Clear(0x14);
        }
    }
}