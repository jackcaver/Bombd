using Bombd.Serialization;
using Bombd.Simulation;

namespace Bombd.Types.GameManager;

public class GameManagerPlayerList : List<GamePlayer>, INetworkWritable
{
    public static readonly int MaxPlayers = 64;

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
            writer.Clear(0x14);
        }
    }
}