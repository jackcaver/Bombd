using System.Xml.Serialization;
using Bombd.Types.Network;

namespace Bombd.Simulation;

[XmlRoot("PlayerState")]
public class PlayerState
{
    [XmlIgnore] public bool HasEventVetoed;
    [XmlIgnore] public bool HasLeaderVetoed;
    [XmlIgnore] public bool IsConnecting;
    [XmlIgnore] public int NetcodeUserId;
    
    [XmlAttribute("nameUID")] public uint NameUid;
    [XmlAttribute("pcId")] public int PlayerConnectId;
    [XmlAttribute("kartId")] public int KartId;
    [XmlAttribute("characterId")] public int CharacterId;
    [XmlAttribute("away")] public int Away;
    [XmlAttribute("mic")] public int Mic;
    [XmlAttribute("styleDrift")] public float KartHandlingDrift;
    [XmlAttribute("styleAccel")] public float KartSpeedAccel;

    [XmlIgnore] public int Flags = PlayerStateFlags.None;
    
    public override string ToString()
    {
        return
            $"PlayerConnect = {PlayerConnectId}, NameUID = {NameUid}, CharacterID = {CharacterId}, KartID = {KartId}, Away = {Away}, Connecting = {IsConnecting}, Mic = {Mic}, HasEventVetoed = {HasEventVetoed}, HasLeaderVetoed = {HasLeaderVetoed}";
    }
}