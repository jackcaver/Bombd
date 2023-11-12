using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Messages;

public struct NetMessageEventResults : INetworkMessage
{
    public NetMessageType Type => NetMessageType.EventResultsFinal;
    public required Platform Platform;

    public int SenderNameUid;
    public string ResultsXml;
    public string Destination;
    public int PostEventDelayTime; // 614885743
    public float PostEventScreenTime;

    public void Write(NetworkWriter writer)
    {
        if (Platform == Platform.ModNation)
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