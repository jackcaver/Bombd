using System.Xml.Serialization;

namespace Bombd.Types.GameBrowser;

public class ServerGameList
{
    [XmlElement("serverGameListHeader")] public GameListHeader Header { get; set; }

    [XmlElement("serverGameList")] public List<GameBrowserGame> Games { get; init; }

    [XmlElement("gameListTimeOfDeath")] public int TimeOfDeath { get; set; }
}