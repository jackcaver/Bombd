using Bombd.Serialization;

namespace Bombd.Types.GameBrowser;

public class GameBrowserGame : INetworkWritable
{
    public int TimeSinceLastPlayerJoined { get; set; }
    public GameBrowserPlayerList Players { get; set; }
    public string GameName { get; set; }
    public string DisplayName { get; set; }
    public GameAttributes Attributes { get; init; }

    public void Write(NetworkWriter writer)
    {
        writer.Write(Players.Count);
        writer.Write(TimeSinceLastPlayerJoined);
        writer.Write(Players);
        writer.Write(GameName);
        writer.Write(DisplayName);
        writer.Write(Attributes);
    }
}