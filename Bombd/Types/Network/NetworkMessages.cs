using Bombd.Helpers;
using Bombd.Serialization;
using Bombd.Types.Network.Messages;

namespace Bombd.Types.Network;

public class NetworkMessages
{
    public const string SimServerName = "SimServer";
    private const int HeaderSize = 0x8;
    public static readonly int SimServerUID = CryptoHelper.StringHash32(SimServerName);

    public static ArraySegment<byte> Pack(NetworkWriter writer, INetworkMessage message)
    {
        writer.Reset();

        writer.Write((byte)message.Type);
        writer.Offset += 3;
        writer.Write(SimServerUID);
        writer.Write(message);

        ArraySegment<byte> payload = writer.ToArraySegment();
        int size = payload.Count;
        payload[2] = (byte)(size >> 8);
        payload[3] = (byte)(size >> 0);

        return payload;
    }

    public static ArraySegment<byte> PackData(NetworkWriter writer, ArraySegment<byte> message, NetMessageType type)
    {
        writer.Reset();

        writer.Write((byte)type);
        writer.Offset += 3;
        writer.Write(SimServerUID);
        writer.Write(message);

        ArraySegment<byte> payload = writer.ToArraySegment();
        int size = payload.Count;
        payload[2] = (byte)(size >> 8);
        payload[3] = (byte)(size >> 0);

        return payload;
    }

    public static ArraySegment<byte> Unpack(NetworkReader reader, out NetMessageType type, out int sender)
    {
        type = (NetMessageType)reader.ReadInt8();
        reader.Offset += 1;
        int size = reader.ReadUInt16();
        sender = reader.ReadInt32();
        return reader.ReadSegment(size - HeaderSize);
    }
}