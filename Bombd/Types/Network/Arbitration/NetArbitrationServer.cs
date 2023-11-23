using System.Numerics;

namespace Bombd.Types.Network.V2;

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