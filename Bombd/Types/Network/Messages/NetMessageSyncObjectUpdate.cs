using Bombd.Serialization;
using Bombd.Types.Network.Simulation;

namespace Bombd.Types.Network.Messages;

public struct NetMessageSyncObjectUpdate : INetworkMessage, INetworkReadable
{
    public NetMessageType Type => NetMessageType.SyncObjectUpdate;

    public int Guid;
    public ArraySegment<byte> Data = ArraySegment<byte>.Empty;
    
    public NetMessageSyncObjectUpdate()
    {
        
    }
    
    public NetMessageSyncObjectUpdate(SyncObject syncObject)
    {
        Guid = syncObject.Guid;
        Data = syncObject.Data;
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(Guid);
        writer.Write(Data.Count);
        writer.Write(Data);
    }

    public void Read(NetworkReader reader)
    {
        Guid = reader.ReadInt32();
        Data = reader.ReadSegment(reader.ReadInt32());
    }
}