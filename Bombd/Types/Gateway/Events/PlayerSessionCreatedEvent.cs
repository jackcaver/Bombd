using System.Text.Json.Serialization;

namespace Bombd.Types.Gateway.Events;

public class PlayerSessionCreatedEvent
{
    [JsonPropertyName("uuid")] public string SessionUuid { get; set; } = string.Empty;
    [JsonPropertyName("id")] public int PlayerConnectId { get; set; }
    [JsonPropertyName("user")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("issuer")] public int Issuer { get; set; }
}