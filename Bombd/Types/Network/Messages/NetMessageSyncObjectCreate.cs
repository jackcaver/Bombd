using Bombd.Serialization;
using Bombd.Types.Network.Simulation;

namespace Bombd.Types.Network.Messages;

public struct NetMessageSyncObjectCreate : INetworkWritable, INetworkReadable
{
    public string OwnerName = string.Empty;
    public string DebugTag = string.Empty;
    public int ObjectType;
    public int Guid;
    
    public NetMessageSyncObjectCreate()
    {
        
    }

    public NetMessageSyncObjectCreate(SyncObject syncObject)
    {
        OwnerName = syncObject.OwnerName;
        DebugTag = syncObject.DebugTag;
        ObjectType = syncObject.Type;
        Guid = syncObject.Guid;
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(OwnerName);
        writer.Write(DebugTag);
        writer.Write(ObjectType);
        writer.Write(Guid);
    }

    public void Read(NetworkReader reader)
    {
        OwnerName = reader.ReadString();
        DebugTag = reader.ReadString();
        ObjectType = reader.ReadInt32();
        Guid = reader.ReadInt32();
    }
}