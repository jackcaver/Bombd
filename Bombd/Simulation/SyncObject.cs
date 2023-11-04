namespace Bombd.Simulation;

public class SyncObject
{
    public string OwnerName { get; set; } = string.Empty;
    public string DebugTag { get; set; } = string.Empty;
    public int OwnerUserId { get; set; }
    public int Guid { get; set; }
    public int Type { get; set; }
    public ArraySegment<byte> Data { get; set; } = ArraySegment<byte>.Empty;
}