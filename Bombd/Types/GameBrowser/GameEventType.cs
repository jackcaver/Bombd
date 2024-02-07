using System.Xml.Serialization;

namespace Bombd.Types.GameBrowser;

public enum GameEventType
{
    [XmlEnum("playerJoined")]
    PlayerJoined,
    [XmlEnum("playerLeft")]
    PlayerLeft,
    [XmlEnum("updatedAttributes")]
    UpdatedAttributes,
    [XmlEnum("gameRemovedNoSubMatch")]
    GameRemovedNoSubMatch,
    [XmlEnum("gameAddedSubMatch")]
    GameAddSubMatch,
    [XmlEnum("gameShutdown")]
    Shutdown,
    [XmlEnum("unknown")]
    Unknown
}