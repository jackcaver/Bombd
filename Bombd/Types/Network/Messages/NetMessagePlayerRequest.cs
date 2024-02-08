using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Messages;

public struct NetMessagePlayerRequest(Platform platform) : INetworkReadable
{
    public uint Target;
    public uint Requester;
    
    public static NetMessagePlayerRequest ReadVersioned(ArraySegment<byte> data, Platform platform)
    {
        var msg = new NetMessagePlayerRequest(platform);
        using var reader = NetworkReaderPool.Get(data);
        msg.Read(reader);
        return msg;
    }
    
    public void Read(NetworkReader reader)
    {
        Target = reader.ReadUInt32();
        if (platform == Platform.Karting)
            Requester = reader.ReadUInt32();
    }
}