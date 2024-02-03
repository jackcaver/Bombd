using System.Xml.Serialization;
using Bombd.Types.GameBrowser;

namespace Bombd.Types.Network;

public class GameEvent
{
    [XmlElement("eventType")]
    public GameEventType Type { get; set; }
    
    [XmlElement("gameinfo")]
    public GameBrowserGame Info { get; set; }
}