using System.Collections;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Bombd.Extensions;
using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Serialization;

namespace Bombd.Types.Services;

public class NetcodeTransaction
{
    private const string RequestTransactionType = "TRANSACTION_TYPE_REQUEST";
    private const string ReplyTransactionType = "TRANSACTION_TYPE_REPLY";

    private readonly Dictionary<string, string> _params = new();

    public NetcodeTransaction(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        ServiceName = document.SelectSingleNode("service").Attributes["name"].Value;
        MethodName = document.SelectSingleNode("service/transaction/method")?.InnerText.Trim().Split(' ')[0];
        TransactionType = document.SelectSingleNode("service/transaction")?.Attributes?["type"].Value;
        TransactionId = int.Parse(document.SelectSingleNode("service/transaction").Attributes?["id"].Value);
        foreach (object? element in document.GetElementsByTagName("param"))
        {
            string name = ((XmlElement)element).GetElementsByTagName("name")[0].InnerText.Trim();
            string value = ((XmlElement)element).GetElementsByTagName("value")[0].InnerText.Trim();
            
            // Just concatenate the results together if there's multiple
            // This really only happens with the guest param in Karting.
            if (_params.TryGetValue(name, out string? existingValue))
                _params[name] = existingValue + "," + value;
            else
                _params[name] = value;
        }
        
        if (MethodName != "logClientMessage")
            Logger.LogTrace<NetcodeTransaction>(xml);
    }

    private NetcodeTransaction(string type, string service, string method)
    {
        ServiceName = service;
        MethodName = method;
        TransactionType = type;
        if (type == RequestTransactionType) TransactionId = TimeHelper.LocalTime;
    }

    public string Error { get; set; } = string.Empty;
    public string MethodName { get; set; }
    public string ServiceName { get; }
    public int TransactionId { get; private init; }
    public string TransactionType { get; set; }

    public string this[string key]
    {
        get => _params.GetValueOrDefault(key, string.Empty);
        set => _params[key] = value;
    }

    public bool TryGet(string key, out string value) => _params.TryGetValue(key, out value);
    public string Get(string key) => _params[key];
    public bool Has(string key) => _params.ContainsKey(key);

    public static NetcodeTransaction MakeRequest(string service, string method) =>
        new(RequestTransactionType, service, method);

    public static NetcodeTransaction MakeRequest(string service, string method, object value)
    {
        var transaction = new NetcodeTransaction(RequestTransactionType, service, method);
        transaction.SetObject(value);
        return transaction;
    }

    public void SetObject(object value)
    {
        try
        {
            foreach (PropertyInfo property in value.GetType().GetProperties())
            {
                object? propertyValue = property.GetValue(value);
                if (propertyValue == null) continue;
                string propertyName = property.Name;
                Type propertyType = property.PropertyType;

                var xmlAttribute = property.GetCustomAttribute<XmlElementAttribute>();
                if (xmlAttribute != null) propertyName = xmlAttribute.ElementName;

                if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    IEnumerable<INetworkWritable> collection = ((IEnumerable)propertyValue).Cast<INetworkWritable>();
                    using NetworkWriterPooled writer = NetworkWriterPool.Get();
                    foreach (INetworkWritable? element in collection) writer.Write(element);
                    _params[propertyName] = Convert.ToBase64String(writer.ToArraySegment());
                }
                else if (propertyValue is INetworkWritable writable)
                {
                    using NetworkWriterPooled writer = NetworkWriterPool.Get();
                    writer.Write(writable);
                    _params[propertyName] = Convert.ToBase64String(writer.ToArraySegment());
                }
                else _params[propertyName] = propertyValue.ToString()!;
            }
        }
        catch (Exception)
        {
            Logger.LogError<NetcodeTransaction>(
                "An error occurred serialization transaction object. Sending error response.");
            Error = "SerializationError";
        }
    }

    public NetcodeTransaction MakeResponse() =>
        new(ReplyTransactionType, ServiceName, MethodName)
        {
            TransactionId = TransactionId
        };

    public ArraySegment<byte> ToArraySegment()
    {
        var document = new XmlDocument();
        XmlElement root = document.CreateXmlElement("service");
        root.SetAttribute("name", ServiceName);
        XmlElement transaction = document.CreateXmlElement(root, "transaction");
        transaction.SetAttribute("id", TransactionId.ToString());
        transaction.SetAttribute("type", TransactionType);
        XmlElement method = document.CreateXmlElement(transaction, "method", MethodName);

        if (string.IsNullOrEmpty(Error))
        {
            foreach (string param in _params.Keys)
            {
                XmlElement node = document.CreateXmlElement(method, "param");
                document.CreateXmlElement(node, "name", param);
                document.CreateXmlElement(node, "value", $" {_params[param]} ");
            }
        }
        else document.CreateXmlElement(method, "error", Error);

        if (MethodName != "logClientMessage")
            Logger.LogTrace<NetcodeTransaction>(document.OuterXml);
        
        return Encoding.UTF8.GetBytes(document.OuterXml);
    }
}