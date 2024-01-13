using System.Xml.Serialization;

namespace Bombd.Types.Network.Races;

public enum LevelType
{
    [XmlEnum("cooperative")] Cooperative,
    [XmlEnum("versus")] Versus
}