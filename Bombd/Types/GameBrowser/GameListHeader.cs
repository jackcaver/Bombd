using Bombd.Serialization;

namespace Bombd.Types.GameBrowser;

public class GameListHeader : INetworkWritable
{
    public int NumGamesInList;
    public int TimeOfDeath;
    public string ClusterUuid = string.Empty;
    public string GameManagerIp = string.Empty;
    public string GameManagerPort = string.Empty;
    public string GameManagerUuid = string.Empty;

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