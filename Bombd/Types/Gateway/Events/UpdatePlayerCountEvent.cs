using System.Text.Json.Serialization;

namespace Bombd.Types.Gateway.Events;

public class UpdatePlayerCountEvent
{
    [JsonPropertyName("count")] public int PlayerCount { get; set; }
}