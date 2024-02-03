using System.Text.Json.Serialization;

namespace Bombd.Types.Gateway.Events;

public class PlayerUpdatedEvent
{
    [JsonPropertyName("id")] public int PlayerConnectId { get; set; }
    [JsonPropertyName("char")] public int CharacterId { get; set; }
    [JsonPropertyName("kart")] public int KartId { get; set; }
}