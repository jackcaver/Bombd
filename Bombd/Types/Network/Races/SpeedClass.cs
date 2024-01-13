using System.Xml.Serialization;

namespace Bombd.Types.Network.Races;

public enum SpeedClass : int
{
    [XmlEnum("fast")] Fast,
    [XmlEnum("faster")] Faster,
    [XmlEnum("fastest")] Fastest
}