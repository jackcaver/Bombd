using Bombd.Protocols;
using JetBrains.Annotations;

namespace Bombd.Attributes;

[AttributeUsage(AttributeTargets.Class)]
[MeansImplicitUse]
public class ServiceAttribute : Attribute
{
    public ServiceAttribute(string name, int port, ProtocolType protocol)
    {
        Name = name;
        Port = port;
        Protocol = protocol;
    }

    public string Name { get; }
    public int Port { get; }
    public ProtocolType Protocol { get; }
}