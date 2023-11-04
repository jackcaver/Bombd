using Bombd.Serialization;

namespace Bombd.Types.Directory;

public class ServiceInstance : INetworkWritable
{
    public string HostName { get; set; }
    public string ServerUuid { get; set; }
    public string Port { get; set; }
    public string Protocol { get; set; }
    public int ConnectOrder { get; set; }
    public int Key { get; set; }

    public void Write(NetworkWriter writer)
    {
        writer.Write(HostName);
        writer.Write(ServerUuid);
        writer.Write(Port);
        writer.Write(Protocol);
        writer.Write(ConnectOrder);
        writer.Write(Key);
    }
}