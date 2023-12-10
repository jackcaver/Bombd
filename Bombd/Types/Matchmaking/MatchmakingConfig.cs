namespace Bombd.Types.Matchmaking;

public class MatchmakingConfig
{
    public int Version;
    public Dictionary<string, SimpleFilter> SimpleFilters = new();
    public Dictionary<string, AdvancedFilter> AdvancedFilters = new();
    public Dictionary<string, NumberPreference> NumberPreferences = new();
    public Dictionary<int, float> ThresholdDecayList = new();
    public List<float> ThresholdDecayTable = new();
    public int ThresholdDecayTimeout;
    public Dictionary<string, MatchSizeDecayTable> MatchSizeDecayTables = new();
    public int LonelinessReductionSeconds;
    public float LonelinessReductionMultiplier;
    public int LonelinessReductionMaxApplications;

}