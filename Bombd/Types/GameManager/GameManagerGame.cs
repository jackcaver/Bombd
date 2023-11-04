using System.Xml.Serialization;
using Bombd.Types.GameBrowser;

namespace Bombd.Types.GameManager;

public class GameManagerGame
{
    [XmlElement("gamename")] public string GameName { get; set; }
    [XmlElement("gamebrowsername")] public string GameBrowserName { get; set; }
    [XmlElement("gameid")] public int GameId { get; set; }
    [XmlElement("numplayerslist")] public int PlayerCount => Players.Count;
    [XmlElement("playerlist")] public GameManagerPlayerList Players { get; set; }
    [XmlElement("attributes")] public GameAttributes Attributes { get; set; }
}