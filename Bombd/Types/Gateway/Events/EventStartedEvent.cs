using System.Text.Json.Serialization;

namespace Bombd.Types.Gateway.Events;

public class EventStartedEvent
{
    [JsonPropertyName("id")] public int TrackId { get; set; }
    [JsonPropertyName("players")] public List<int> PlayerIds { get; set; } = [];
}