using Bombd.Serialization;
using Bombd.Types.Network.Simulation;

namespace Bombd.Types.Network.Messages;

public struct NetMessageSyncObjectRemove : INetworkWritable, INetworkReadable
{
    public int Guid;
    
    public NetMessageSyncObjectRemove()
    {
        
    }
    
    public NetMessageSyncObjectRemove(SyncObject syncObject)
    {
        Guid = syncObject.Guid;
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(Guid);
    }

    public void Read(NetworkReader reader)
    {
        Guid = reader.ReadInt32();
    }
}