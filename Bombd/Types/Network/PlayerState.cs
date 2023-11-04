using System.Xml.Serialization;

namespace Bombd.Types.Network;

[XmlRoot("PlayerState")]
public class PlayerState
{
    [XmlIgnore] public bool ReadyForNis;
    [XmlIgnore] public bool ReadyForEvent;
    [XmlAttribute("characterId")] public uint CharacterId;
    [XmlAttribute("nameUID")] public uint NameUid;
    [XmlAttribute("kartId")] public uint KartId;
    [XmlAttribute("away")] public uint Away;
    [XmlAttribute("mic")] public uint Mic;
    [XmlAttribute("pcId")] public uint PlayerConnectId;
}