namespace Bombd.Types.Matchmaking;

public class MatchSizeDecayTable
{
    public string TableTypeName = string.Empty;
    public List<int> DecayTable = new();
    public int MaxMatchSize;
    public bool IsDefault;
}