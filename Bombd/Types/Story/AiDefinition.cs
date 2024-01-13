using System.Xml.Serialization;

namespace Bombd.Types.Story;

[XmlRoot("Ai")]
[XmlType("Ai")]
public class AiDefinition
{
    [XmlAttribute("name")] public string Name { get; set; } = "Codsworth";
    [XmlAttribute("profile")] public string Profile { get; set; } = "MID";
}