using Bombd.Helpers;
using Bombd.Serialization;
using Bombd.Types.Network.Arbitration;

namespace Bombd.Types.Network.Messages;

public class NetMessageArbitratedItemBlock(Platform platform) : INetworkWritable, INetworkReadable
{
    public int ItemType;
    public uint PlayerNameUid;
    public AcquireBehavior AcquireBehavior = AcquireBehavior.SingleAcquire;
    public List<int> ItemIds = [];
    
    public static NetMessageArbitratedItemBlock ReadVersioned(ArraySegment<byte> data, Platform platform)
    {
        var message = new NetMessageArbitratedItemBlock(platform);
        using var reader = NetworkReaderPool.Get(data);
        message.Read(reader);
        return message;
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(ItemIds.Count);
        if (platform == Platform.ModNation)
        {
            foreach (int itemId in ItemIds)
            {
                writer.Write(PlayerNameUid);
                writer.Write(itemId);
            }
        }
        else
        {
            writer.Write(ItemType);
            writer.Write(PlayerNameUid);
            writer.Write((int) AcquireBehavior);
            foreach (int itemId in ItemIds)
                writer.Write(itemId);
        }
    }
    
    public void Read(NetworkReader reader)
    {
        int count = reader.ReadInt32();
        ItemIds = new List<int>(count);
        if (platform == Platform.ModNation)
        {
            ItemType = 0;
            AcquireBehavior = AcquireBehavior.SingleAcquire;
            for (int i = 0; i < count; ++i)
            {
                // Each ItemId has a different owner in ModNation, but from what I've seen
                // it's generally always the same (0) anyway.
                PlayerNameUid = reader.ReadUInt32(); 
                ItemIds.Add(reader.ReadInt32());
            }
        }
        else
        {
            ItemType = reader.ReadInt32();
            PlayerNameUid = reader.ReadUInt32();
            AcquireBehavior = (AcquireBehavior) reader.ReadInt32();
            for (int i = 0; i < count; ++i)
                ItemIds.Add(reader.ReadInt32());
        }
    }
}