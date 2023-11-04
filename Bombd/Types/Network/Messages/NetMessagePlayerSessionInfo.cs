using Bombd.Serialization;

namespace Bombd.Types.Network.Messages;

public struct NetMessagePlayerSessionInfo : INetworkMessage
{
    public NetMessageType Type => NetMessageType.PlayerSessionInfo;

    public int JoinStatus;
    public int UserId;
    public int PlayerId;
    public int NameUid;

    public void Write(NetworkWriter writer)
    {
        writer.Write(JoinStatus);
        writer.Write(UserId);

        // These don't even get used in Modnation
        writer.Write(PlayerId);
        writer.Write(NameUid);
    }
}