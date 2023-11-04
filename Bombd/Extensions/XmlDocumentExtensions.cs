using System.Xml;

namespace Bombd.Extensions;

public static class XmlDocumentExtensions
{
    public static XmlElement CreateXmlElement(this XmlDocument document, string name, string? text = null)
    {
        XmlElement element = document.CreateElement(name);
        if (!string.IsNullOrEmpty(text))
            element.InnerText = text;
        document.AppendChild(element);
        return element;
    }

    public static XmlElement CreateXmlElement(this XmlDocument document, XmlElement parent, string name,
        string? text = null)
    {
        XmlElement element = document.CreateElement(name);
        if (!string.IsNullOrEmpty(text))
            element.InnerText = text;
        parent.AppendChild(element);
        return element;
    }
}