using Bombd.Helpers;
using Bombd.Serialization;
using Bombd.Types.Network.Room;

namespace Bombd.Types.Network.Messages;

public struct NetMessagePlayerLeave : INetworkMessage, INetworkReadable
{
    public NetMessageType Type => NetMessageType.PlayerLeave;

    public readonly Platform Platform;
    public LeaveReason Reason;
    public string PlayerName = string.Empty;
    
    public NetMessagePlayerLeave(Platform platform) => Platform = platform;
    
    public static NetMessagePlayerLeave ReadVersioned(ArraySegment<byte> data, Platform platform)
    {
        var msg = new NetMessagePlayerLeave(platform);
        using var reader = NetworkReaderPool.Get(data);
        msg.Read(reader);
        return msg;
    }
    
    public void Read(NetworkReader reader)
    {
        Reason = (LeaveReason)reader.ReadInt32();
        PlayerName = Platform == Platform.Karting ? reader.ReadString() : reader.ReadString(0x20);
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write((int)Reason);
        if (Platform == Platform.Karting) writer.Write(PlayerName);
        else writer.Write(PlayerName, 0x20);
    }
}