using Bombd.Serialization;

namespace Bombd.Types.Network.Messages;

public struct NetMessageSyncObjectCreate : INetworkMessage, INetworkReadable
{
    public NetMessageType Type => NetMessageType.SyncObjectCreate;

    public string OwnerName;
    public string DebugTag;
    public int ObjectType;
    public NetObjectMessageType MessageType;
    public int Guid;
    public ArraySegment<byte> Data;

    public void Write(NetworkWriter writer)
    {
        writer.Write(Guid);
        writer.Write((int)MessageType);
        switch (MessageType)
        {
            case NetObjectMessageType.Create:
                writer.Write(OwnerName, 0x20);
                writer.Write(DebugTag, 0x20);
                writer.Write(ObjectType);
                writer.Write(Guid);
                break;
            case NetObjectMessageType.Update:
                writer.Write(Data);
                break;
            case NetObjectMessageType.Remove:
                break;
        }
    }

    public void Read(NetworkReader reader)
    {
        Guid = reader.ReadInt32();
        MessageType = (NetObjectMessageType)reader.ReadInt32();
        switch (MessageType)
        {
            case NetObjectMessageType.Create:
            {
                OwnerName = reader.ReadString(0x20);
                DebugTag = reader.ReadString(0x20);
                ObjectType = reader.ReadInt32();
                break;
            }
            case NetObjectMessageType.Update:
            {
                Data = reader.ReadSegment(reader.Capacity - reader.Offset);
                break;
            }
        }
    }
}