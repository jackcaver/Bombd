using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Messages;

public struct NetMessageEventResults(Platform platform) : INetworkReadable, INetworkWritable
{
    public uint SenderNameUid;
    public string ResultsXml;
    public string Destination;
    public int PostEventDelayTime;
    public float PostEventScreenTime;
    
    public static NetMessageEventResults ReadVersioned(ArraySegment<byte> data, Platform platform)
    {
        var message = new NetMessageEventResults(platform);
        using var reader = NetworkReaderPool.Get(data);
        message.Read(reader);
        return message;
    }
    
    public void Read(NetworkReader reader)
    {
        if (platform == Platform.ModNation)
        {
            ResultsXml = reader.ReadString(0x1800);
            Destination = reader.ReadString(0x40);
            PostEventDelayTime = reader.ReadInt32();
            PostEventScreenTime = reader.ReadInt32();
            return;
        }

        SenderNameUid = reader.ReadUInt32();
        PostEventDelayTime = reader.ReadInt32();
        PostEventScreenTime = reader.ReadSingle();
        ResultsXml = reader.ReadString();
        Destination = reader.ReadString();
    }

    public void Write(NetworkWriter writer)
    {
        if (platform == Platform.ModNation)
        {
            writer.Write(ResultsXml, 0x1800);
            writer.Write(Destination, 0x40);
            writer.Write(PostEventDelayTime);
            writer.Write(PostEventScreenTime);
            return;
        }
        
        writer.Write(SenderNameUid);
        writer.Write(PostEventDelayTime);
        writer.Write(PostEventScreenTime);
        writer.Write(ResultsXml);
        writer.Write(Destination);
    }
}