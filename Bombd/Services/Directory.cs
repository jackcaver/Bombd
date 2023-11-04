using Bombd.Attributes;
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
        // I'm fairly sure the session key is just the session id that was
        // returned from the login on the player connect side of things.
        // But we don't use session keys in this implementation, should we?
        // I figure connection id is enough, the only issue would be potential
        // UDP spoofing on the gameserver side of things, but if we send back a unique
        // hash salt then that's less of a concern, although maybe we should use the session id
        // field there, I don't know, look more into this at a later date.

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