using Bombd.Serialization;

namespace Bombd.Types.Network.Messages;

public struct NetMessagePlayerLeave : INetworkMessage, INetworkReadable
{
    public NetMessageType Type => NetMessageType.PlayerLeave;
    
    public LeaveReason Reason;
    public string PlayerName;

    public void Read(NetworkReader reader)
    {
        Reason = (LeaveReason)reader.ReadInt32();
        PlayerName = reader.ReadString(0x20);
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write((int)Reason);
        writer.Write(PlayerName, 0x20);
    }
}