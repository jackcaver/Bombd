using Bombd.Serialization;
using Bombd.Types.Network.Room;

namespace Bombd.Types.Network.Messages;

public struct NetMessagePlayerSessionInfo : INetworkMessage
{
    public NetMessageType Type => NetMessageType.PlayerSessionInfo;

    public GameSessionStatus JoinStatus;
    public int UserId;
    public int PlayerId;
    public uint NameUid;

    public void Write(NetworkWriter writer)
    {
        writer.Write((int)JoinStatus);
        writer.Write(UserId);

        // These don't even get used in Modnation
        writer.Write(PlayerId);
        writer.Write(NameUid);
    }
}