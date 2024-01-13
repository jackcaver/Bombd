namespace Bombd.Types.Network.Arbitration;

public class NetArbitrationServer
{
    public List<ItemNode> Items = new();
    public List<NetActionNode> Actions = new();
    public List<NetPlayerNode> Players = new();
    public bool IsQueuedCharData;
    public Mutex QueuedCharDataMutex = new();
    
    // On ArbitratedItemCreateBlock
        // Initialize all ItemNodes
    
}