using System.Xml;
using System.Xml.Serialization;

namespace Bombd.Types.Network.Races;

[XmlRoot("SeriesResult")]
public class SeriesResult
{
    // This class is serialized without namespaces and multiple root elements
    // which is a pain by default.
    private static XmlSerializer _serializer = new(typeof(SeriesResult));
    private static XmlWriterSettings _xmlWriterSettings = new()
    {
        ConformanceLevel = ConformanceLevel.Fragment, 
        OmitXmlDeclaration = true,
        Indent = false,
        NamespaceHandling = NamespaceHandling.OmitDuplicates
    };
    private static XmlSerializerNamespaces _xmlNamespaces = new(new[] { XmlQualifiedName.Empty });
    
    [XmlAttribute("ownerName")] public string OwnerName = string.Empty;
    [XmlAttribute("totalPoints")] public int TotalPoints;
    [XmlAttribute("pointsEarned")] public int PointsEarned;
    [XmlAttribute("isDNFed")] public int DidNotFinish;
    [XmlAttribute("ranking")] public int Ranking;

    public static string Serialize(List<SeriesResult> results)
    {
        using var stream = new StringWriter();
        stream.Write("<?xml version=\"1.0\"?>\n");
        using var writer = XmlWriter.Create(stream, _xmlWriterSettings);
        foreach (SeriesResult result in results)
        {
            writer.WriteRaw("\n");
            _serializer.Serialize(writer, result, _xmlNamespaces);  
        }
        stream.Write("\0");
        return stream.ToString();
    }
}