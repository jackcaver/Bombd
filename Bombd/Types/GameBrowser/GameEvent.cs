using System.Xml.Serialization;

namespace Bombd.Types.GameBrowser;

public class GameEvent
{
    [XmlElement("eventType")]
    public GameEventType Type { get; set; }
    
    [XmlElement("gameinfo")]
    public GameBrowserGame Info { get; set; }
}