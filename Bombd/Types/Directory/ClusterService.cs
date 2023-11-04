using Bombd.Serialization;

namespace Bombd.Types.Directory;

public class ClusterService : INetworkWritable
{
    public string ServiceName { get; set; }
    public List<ServiceInstance> Services { get; set; } = new();

    public void Write(NetworkWriter writer)
    {
        writer.Write(ServiceName);
        writer.Write(Services.Count);
        foreach (ServiceInstance service in Services) writer.Write(service);
    }
}