using System.Text.Json.Serialization;

namespace Bombd.Types.Gateway;

public class PlayerEventStats
{
    [JsonPropertyName("id")] public int PlayerConnectId { get; set; }
    [JsonPropertyName("hang")] public float BestHangTime { get; set; }
    [JsonPropertyName("drift")] public float BestDrift { get; set; }
    [JsonPropertyName("finished")] public bool Finished { get; set; }
    [JsonPropertyName("rank")] public int Rank { get; set; }
    [JsonPropertyName("playgroupSize")] public int PlaygroupSize { get; set; }
    [JsonPropertyName("finish")] public float FinishTime { get; set; }
    [JsonPropertyName("lap")] public float BestLapTime { get; set; }
    [JsonPropertyName("score")] public float Points { get; set; }
}