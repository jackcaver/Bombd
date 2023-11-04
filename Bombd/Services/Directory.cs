using Bombd.Attributes;
using Bombd.Core;
using Bombd.Protocols;
using Bombd.Types.Directory;
using Bombd.Types.Services;

namespace Bombd.Services;

/// <summary>
///     Service that tells the client about the other services available on this cluster.
/// </summary>
[Service("directory", 10501, ProtocolType.TCP)]
public class Directory : BombdService
{
    /// <summary>
    ///     Returns a list of all available services on this cluster.
    /// </summary>
    [Transaction("getServiceList")]
    public ClusterInfo GetServiceList(TransactionContext context)
    {
        var info = new ClusterInfo
        {
            ClusterUuid = Bombd.ClusterUuid
        };

        foreach (BombdService service in Bombd.Services)
        {
            info.Services.Add(new ClusterService
            {
                ServiceName = service.Name,
                Services =
                {
                    new ServiceInstance
                    {
                        HostName = Bombd.Configuration.ExternalIP,
                        ServerUuid = service.Uuid,
                        Port = service.Port.ToString(),
                        Protocol = service.Protocol.ToString(),
                        ConnectOrder = 0,
                        Key = context.Connection.SessionId
                    }
                }
            });
        }

        return info;
    }
}