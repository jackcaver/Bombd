using System.Text;
using System.Xml.Serialization;
using Bombd.Helpers;

namespace Bombd.Types.Network;

[XmlRoot("NetChatMessage")]
public class NetChatMessage
{
    private static readonly XmlSerializer StateSerializer = new(typeof(NetChatMessage));
    
    [XmlAttribute("senderName")] public string Sender;
    [XmlAttribute("recipientName")] public string Recipient;
    [XmlAttribute("body")] public string Body;
    [XmlAttribute("isPrivate")] public int Private;
    
    public static NetChatMessage LoadXml(ArraySegment<byte> data, Platform platform)
    {
        string xml;
        if (platform == Platform.Karting)
        {
            int len = 0;
            len |= (data[0] << 24);
            len |= (data[1] << 16);
            len |= (data[2] << 8);
            len |= (data[3] << 0);

            if (data.Count != 4 + len)
                throw new ArgumentException("NetChatMessage packet is malformed!");
            
            xml = Encoding.ASCII.GetString(data.Array!, data.Offset + 4, len - 1);
        }
        else
        {
            int len = 0;
            while (len < data.Count && data[len] != 0)
                len++;
            
            xml = len == 0 ? string.Empty : Encoding.ASCII.GetString(data.Array!, data.Offset, len);
        }

        using var reader = new StringReader(xml);
        return (NetChatMessage) StateSerializer.Deserialize(reader)!;
    }
}