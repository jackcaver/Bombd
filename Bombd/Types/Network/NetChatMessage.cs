using System.Text;
using System.Xml.Serialization;

namespace Bombd.Types.Network;

[XmlRoot("NetChatMessage")]
public class NetChatMessage
{
    private static readonly XmlSerializer StateSerializer = new(typeof(NetChatMessage));
    
    [XmlAttribute("senderName")] public string Sender;
    [XmlAttribute("recipientName")] public string Recipient;
    [XmlAttribute("body")] public string Body;
    [XmlAttribute("isPrivate")] public int Private;
    
    public static NetChatMessage LoadXml(ArraySegment<byte> data)
    {
        int len = 0;
        while (len < data.Count && data[len] != 0)
            len++;
        
        string xml = len == 0 ? string.Empty : Encoding.ASCII.GetString(data.Array!, data.Offset, len);
        using var reader = new StringReader(xml);
        return (NetChatMessage) StateSerializer.Deserialize(reader)!;
    }
}