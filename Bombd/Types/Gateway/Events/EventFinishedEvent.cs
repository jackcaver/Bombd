using System.Text.Json.Serialization;

namespace Bombd.Types.Gateway.Events;

public class EventFinishedEvent
{
    [JsonPropertyName("id")] public int TrackId { get; set; }
    [JsonPropertyName("stats")] public List<PlayerEventStats> Stats { get; set; } = [];
    [JsonPropertyName("isMNR")] public bool IsMNR { get; set; }
    [JsonPropertyName("gameType")] public string GameType { get; set; } = "";
    [JsonPropertyName("ranked")] public bool IsRanked { get; set; }
}