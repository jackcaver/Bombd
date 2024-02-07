using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Messages;

public struct NetMessageArbitratedItem(Platform platform) : INetworkWritable, INetworkReadable
{
    public int ItemType;
    public int ItemId;
    public uint PlayerNameUid;
    public int Timeout;
    public int Param;
    public int Flags;
    
    public static NetMessageArbitratedItem ReadVersioned(ArraySegment<byte> data, Platform platform)
    {
        var message = new NetMessageArbitratedItem(platform);
        using var reader = NetworkReaderPool.Get(data);
        message.Read(reader);
        return message;
    }
    
    public void Read(NetworkReader reader)
    {
        ItemType = reader.ReadInt32();
        ItemId = reader.ReadInt32();
        PlayerNameUid = reader.ReadUInt32();
        Timeout = reader.ReadInt32();
        if (platform == Platform.Karting)
        {
            Param = reader.ReadInt32();
            Flags = reader.ReadInt32();   
        }
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write(ItemType);
        writer.Write(ItemId);
        writer.Write(PlayerNameUid);
        writer.Write(Timeout);
        // None of the data here is used in serialization, it seems
        // to just be them allocating the class on the stack
        if (platform == Platform.ModNation) writer.Clear(0x48);
        else
        {
            writer.Write(Param);
            writer.Write(Flags);   
        }
    }
}