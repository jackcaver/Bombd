using System.Xml.Serialization;

namespace Bombd.Simulation;

[XmlRoot("PlayerState")]
public class PlayerState
{
    [XmlAttribute("away")] public uint Away;
    [XmlAttribute("characterId")] public uint CharacterId;
    [XmlAttribute("kartId")] public uint KartId;
    [XmlAttribute("mic")] public uint Mic;
    [XmlAttribute("nameUID")] public uint NameUid;
    [XmlAttribute("pcId")] public uint PlayerConnectId;
    [XmlIgnore] public bool ReadyForEvent;
    [XmlIgnore] public bool ReadyForNis;
}