using Bombd.Serialization;

namespace Bombd.Types.GameBrowser;

public class GameListHeader : INetworkWritable
{
    public int NumGamesInList { get; set; }
    public int TimeOfDeath { get; set; }
    public string ClusterUuid { get; set; }
    public string GameManagerIp { get; set; }
    public string GameManagerPort { get; set; }
    public string GameManagerUuid { get; set; }

    public void Write(NetworkWriter writer)
    {
        writer.Write(NumGamesInList);
        writer.Write(TimeOfDeath);

        writer.Write(ClusterUuid);
        writer.Write(GameManagerIp);
        writer.Write(GameManagerPort);
        writer.Write(GameManagerUuid);
    }
}