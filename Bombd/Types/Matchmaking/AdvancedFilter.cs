namespace Bombd.Types.Matchmaking;

public class AdvancedFilter : SimpleFilter
{
    public float Weight;
    public string LocalValue = string.Empty;
    public bool Absolute;
}