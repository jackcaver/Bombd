using System.Xml.Serialization;
using Bombd.Types.Network.Races;

namespace Bombd.Types.Story;

[XmlType("Track")]
[XmlRoot("Track")]
public class CareerTrack
{
    [XmlAttribute("name")] public string Name { get; set; } = string.Empty;
    [XmlAttribute("id")] public int Id { get; set; }
    [XmlAttribute("class")] public SpeedClass Class { get; set; } = SpeedClass.Faster;
    [XmlAttribute("type")] public RaceType Type { get; set; } = RaceType.Action;
    [XmlAttribute("minplayers")] public int MinPlayers { get; set; } = 4;
    [XmlAttribute("maxplayers")] public int MaxPlayers { get; set; } = 12;
    [XmlAttribute("scoreboard")] public ScoreboardType ScoreboardType { get; set; } = ScoreboardType.Score;
    [XmlAttribute("leveltype")] public LevelType LevelType { get; set; } = LevelType.Cooperative;
    [XmlAttribute("votable")] public bool Votable { get; set; } = true;
}