using System.Text.Json.Serialization;

namespace Bombd.Types.Gateway.Events;

public class ServerInfoEvent
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("address")] public string Address { get; set; } = string.Empty;
    [JsonPropertyName("port")] public int Port { get; set; }
    [JsonPropertyName("key")] public string ServerPrivateKey { get; set; } = string.Empty;
}
