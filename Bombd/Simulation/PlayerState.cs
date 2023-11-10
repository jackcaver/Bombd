using System.Xml.Serialization;
using Bombd.Types.Network;

namespace Bombd.Simulation;

[XmlRoot("PlayerState")]
public class PlayerState
{
    [XmlIgnore] public bool HasEventVetoed;
    [XmlIgnore] public bool HasLeaderVetoed;
    [XmlIgnore] public bool IsConnecting;
    [XmlIgnore] public int KartHandlingDrift;
    [XmlIgnore] public int KartSpeedAccel;
    
    [XmlAttribute("away")] public uint Away;
    [XmlAttribute("characterId")] public uint CharacterId;
    [XmlAttribute("kartId")] public uint KartId;
    [XmlAttribute("mic")] public uint Mic;
    [XmlAttribute("nameUID")] public uint NameUid;
    [XmlAttribute("pcId")] public uint PlayerConnectId;

    [XmlIgnore] public int Flags = PlayerStateFlags.None;

    public override string ToString()
    {
        return
            $"PlayerConnect = {PlayerConnectId}, NameUID = {NameUid}, CharacterID = {CharacterId}, KartID = {KartId}";
    }
}