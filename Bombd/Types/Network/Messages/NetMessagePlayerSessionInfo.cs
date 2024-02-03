using Bombd.Serialization;
using Bombd.Types.Network.Room;

namespace Bombd.Types.Network.Messages;

public struct NetMessagePlayerSessionInfo : INetworkMessage
{
    public NetMessageType Type => NetMessageType.PlayerSessionInfo;

    public GameSessionStatus JoinStatus;
    public int UserId;
    
    public NetMessagePlayerSessionInfo(GameSessionStatus status)
    {
        JoinStatus = status;
    }

    public NetMessagePlayerSessionInfo(GameSessionStatus status, int userId)
    {
        JoinStatus = status;
        UserId = userId;
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write((int)JoinStatus);
        writer.Write(UserId);
        // These two fields are technically PlayerId and NameUid, but ModNation doesn't use them,
        // so I'm not going to bother keeping track of them
        writer.Clear(0x8);
    }
}