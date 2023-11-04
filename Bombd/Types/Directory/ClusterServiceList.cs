using Bombd.Serialization;

namespace Bombd.Types.Directory;

public class ClusterServiceList : List<ClusterService>, INetworkWritable
{
    private const int MaxServices = 8;

    public ClusterServiceList() : base(MaxServices)
    {
    }

    public void Write(NetworkWriter writer)
    {
        foreach (ClusterService service in this) service.Write(writer);
        writer.Clear(8 * (MaxServices - Count));
    }
}