namespace Bombd.Types.Network.V2;

public class ItemNode
{
    public int TypeId;
    public AcquireBehavior AcquireBehavior;
    public int Uid;
    public int OwnerUid;
    public List<ItemAcquirerNode> Acquirers;
}