using System.Xml.Serialization;

namespace Bombd.Types.Network.Races;

public enum ScoreboardType
{
    [XmlEnum("score")] Score,
    [XmlEnum("time")] Time
}