namespace Bombd.Types.Matchmaking;

public class NumberPreference
{
    public string Name = string.Empty;
    public float Range;
    public float Weight;
    public float LocalValue;
    public float DefaultValue;
    public bool Absolute;
}