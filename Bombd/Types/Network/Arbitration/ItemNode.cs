namespace Bombd.Types.Network.Arbitration;

public class ItemNode
{
    public int TypeId;
    public AcquireBehavior AcquireBehavior = AcquireBehavior.SingleAcquire;
    public int Uid;
    public uint OwnerUid;
    public readonly List<ItemAcquirerNode> Acquirers = [];
}