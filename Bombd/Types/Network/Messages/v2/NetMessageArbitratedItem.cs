using Bombd.Serialization;

namespace Bombd.Types.Network.Messages.v2;

public struct NetMessageArbitratedItem : INetworkReadable, INetworkWritable
{
    public int ItemType;
    public int ItemId;
    public uint PlayerNameUid;
    public int Timeout;
    public int Param;
    public int Flags;
    
    public void Read(NetworkReader reader)
    {
        ItemType = reader.ReadInt32();
        ItemId = reader.ReadInt32();
        PlayerNameUid = reader.ReadUInt32();
        Timeout = reader.ReadInt32();
        Param = reader.ReadInt32();
        Flags = reader.ReadInt32();
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write(ItemType);
        writer.Write(ItemId);
        writer.Write(PlayerNameUid);
        writer.Write(Timeout);
        writer.Write(Param);
        writer.Write(Flags);
    }
}