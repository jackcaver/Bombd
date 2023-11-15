using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.GameBrowser;

public class GameBrowserGame : INetworkWritable
{
    public Platform Platform;
    public int TimeSinceLastPlayerJoined;
    public GameBrowserPlayerList Players = new();
    public string GameName = string.Empty;
    public string DisplayName = string.Empty;
    public GameAttributes Attributes = new();
    public int NumFreeSlots = 8;
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(Players.Count);
        writer.Write(TimeSinceLastPlayerJoined);
        writer.Write(Players);
        writer.Write(GameName);
        writer.Write(DisplayName);
        writer.Write(Attributes);
        if (Platform == Platform.Karting)
            writer.Write(NumFreeSlots);
    }
}