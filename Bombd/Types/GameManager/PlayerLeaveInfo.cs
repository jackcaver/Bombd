using System.Xml.Serialization;

namespace Bombd.Types.GameManager;

public class PlayerLeaveInfo
{
    [XmlElement("gamename")] public string GameName { get; set; } = string.Empty;
    [XmlElement("playername")] public string PlayerName { get; set; } = string.Empty;
    [XmlElement("reason")] public string Reason { get; set; } = string.Empty;
}