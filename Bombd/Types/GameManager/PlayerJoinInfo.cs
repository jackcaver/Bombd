using System.Xml.Serialization;

namespace Bombd.Types.GameManager;

public class PlayerJoinInfo
{
    [XmlElement("playername")] public string PlayerName { get; set; } = string.Empty;
    [XmlElement("playerid")] public int PlayerId { get; set; }
    [XmlElement("userid")] public int UserId { get; set; }
    [XmlElement("numGuests")] public int GuestCount { get; set; }
    [XmlElement("p2pAddr")] public string P2PAddress { get; set; } = string.Empty;
    [XmlElement("p2pPort")] public string P2PPort { get; set; } = string.Empty;
    [XmlElement("p2pAddrPrivate")] public string P2PAddressPrivate { get; set; } = string.Empty;
    [XmlElement("p2pPortPrivate")] public string P2PPortPrivate { get; set; } = string.Empty;
}