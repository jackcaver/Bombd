using Bombd.Serialization;

namespace Bombd.Types.Directory;

public class ClusterService : INetworkWritable
{
    public string ServiceName = string.Empty;
    public readonly List<ServiceInstance> Services = new();

    public void Write(NetworkWriter writer)
    {
        writer.Write(ServiceName);
        writer.Write(Services.Count);
        foreach (ServiceInstance service in Services) writer.Write(service);
    }
}