using Bombd.Serialization;

namespace Bombd.Types.Network.Messages;

public struct NetMessageWorldObjectCreate : INetworkMessage, INetworkReadable
{
    public NetMessageType Type => NetMessageType.WorldObjectCreate;

    public int MessageType;
    public int EmitterUid;
    public List<int> Uids;
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(Uids.Count);
        writer.Write(MessageType);
        writer.Write(EmitterUid);
        foreach (int uid in Uids) writer.Write(uid);
    }

    public void Read(NetworkReader reader)
    {
        int count = reader.ReadInt32();
        MessageType = reader.ReadInt32();
        EmitterUid = reader.ReadInt32();
        
        Uids = new List<int>(count);
        for (int i = 0; i < count; ++i)
            Uids.Add(reader.ReadInt32());
    }
}