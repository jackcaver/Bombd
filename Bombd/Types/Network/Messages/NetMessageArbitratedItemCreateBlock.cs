using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Messages;

public class NetMessageArbitratedItemCreateBlock : INetworkMessage
{
    
    public NetMessageType Type => NetMessageType.ArbitratedItemCreateBlock;
    public required Platform Platform;
    
    public int ItemType;
    public List<ItemBlock> Blocks;
    
    public struct ItemBlock
    {
        public int PlayerNameUid;
        public int ItemId;
    }
    
    public void Write(NetworkWriter writer)
    {
        
    }

    public void Read(NetworkReader reader)
    {
        // MODNATION
            // int mItemBlockCount
            // struct[mItemBlockCount] Blocks
                // int mPlayerNameUID
                // int mItemId
                
        // KARTING
            // int mItemBlockCount
            // int mItemType
            // int mPlayerNameUID
            // int mAcquireBehaviour
            // int[mItemBlockCount] mItemIds
    }
}