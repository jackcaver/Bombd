namespace Bombd.Types.Network.Arbitration;

public struct NetActionNode
{
    public int ActionArbitratedIndex;
    public int OwnerUid;
    public bool ActionEnded;
    public int StartTime;
    public int MaxAge;
}