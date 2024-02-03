using System.Text.Json.Serialization;

namespace Bombd.Types.Gateway.Events;

public class PlayerQuitEvent
{
    [JsonPropertyName("id")] public int PlayerConnectId { get; set; }
    [JsonPropertyName("disconnect")] public bool Disconnected { get; set; }
}