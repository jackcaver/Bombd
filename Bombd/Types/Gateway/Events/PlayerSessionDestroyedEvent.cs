using System.Text.Json.Serialization;

namespace Bombd.Types.Gateway.Events;

public class PlayerSessionDestroyedEvent
{
    [JsonPropertyName("uuid")] public string SessionUuid { get; set; } = string.Empty;
}