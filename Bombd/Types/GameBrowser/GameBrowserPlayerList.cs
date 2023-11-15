using Bombd.Serialization;
using Bombd.Simulation;

namespace Bombd.Types.GameBrowser;

public class GameBrowserPlayerList : List<GamePlayer>, INetworkWritable
{
    private const int MaxPlayers = 64;

    public GameBrowserPlayerList() : base(MaxPlayers)
    {
    }

    public GameBrowserPlayerList(IEnumerable<GamePlayer> players) : base(players)
    {
    }

    public void Write(NetworkWriter writer)
    {
        foreach (GamePlayer player in this)
        {
            writer.Write(player.Username);
            writer.Write(player.Guests.Count);
            foreach (GameGuest guest in player.Guests) writer.Write(guest.Username);
        }
    }
}