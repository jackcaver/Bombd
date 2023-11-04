using System.Xml.Serialization;

namespace Bombd.Types.Directory;

public class ClusterInfo
{
    [XmlElement("ClusterUUID")] public string ClusterUuid { get; init; }

    [XmlElement("servicesList")] public ClusterServiceList Services { get; init; } = new();
}