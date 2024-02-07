using Bombd.Serialization;
using Bombd.Types.Network.Room;

namespace Bombd.Types.Network.Messages;

public struct NetMessageSessionInfo : INetworkWritable
{
    public PlayerSessionOperation Operation;
    public int UserId;
    
    public NetMessageSessionInfo(PlayerSessionOperation operation)
    {
        Operation = operation;
    }

    public NetMessageSessionInfo(PlayerSessionOperation operation, int userId)
    {
        Operation = operation;
        UserId = userId;
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write((int)Operation);
        writer.Write(UserId);
        // These two fields are technically PlayerId and NameUid, but ModNation doesn't use them,
        // so I'm not going to bother keeping track of them
        writer.Clear(0x8);
    }
}