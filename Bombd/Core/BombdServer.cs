using System.Runtime.CompilerServices;
using Bombd.Helpers;
using Bombd.Services;

namespace Bombd.Core;

public class BombdServer
{
    /// <summary>
    ///     Cache of service types associated with this server
    /// </summary>
    /// <remarks>
    ///     Used for quickly looking up services anywhere in the program by their
    ///     type rather than searching for a service name in a list.
    /// </remarks>
    private readonly Dictionary<Type, BombdService> _serviceCache = new();

    public BombdServer(BombdConfiguration configuration)
    {
        Configuration = configuration;

        // I don't believe the gameserver ever actually gets directly sent
        // via directory, plus GameManager and GameBrowser are going to need
        // access to it anyway.
        GameServer = CreateService<GameServer>();
    }

    public BombdConfiguration Configuration { get; }

    public string ClusterUuid { get; } = CryptoHelper.GetRandomUUID();
    public List<BombdService> Services { get; } = new();
    public GameServer GameServer { get; }
    public RoomManager RoomManager { get; } = new();
    public SessionManager SessionManager { get; } = new();

    public T GetService<T>() where T : BombdService => (T)_serviceCache[typeof(T)];

    private T CreateService<T>() where T : BombdService, new()
    {
        // It might be better to just make this class static or just cave and put in a
        // constructor that includes the BombdServer, I just think it's nice to
        // not have to repeat it. But might just remove it, because this is somewhat gross?
        Type type = typeof(T);
        var service = (T)RuntimeHelpers.GetUninitializedObject(type);
        type.GetProperty("Bombd")!.SetValue(service, this);
        type.GetConstructor(Type.EmptyTypes)!.Invoke(service, null);
        return service;
    }

    public void AddService<T>() where T : BombdService, new()
    {
        var service = CreateService<T>();
        _serviceCache.Add(typeof(T), service);

        // This is kind of a weird structure, but I wanted to keep
        // it accurate with how the game wants to receive it,
        // rather than hiding away anything, even if it's somewhat
        // unused in this software's use-case.
        // Info.ServicesList.Services.Add(new ClusterService
        // {
        //     ServiceName = service.Name,
        //     Services =
        //     {
        //         new ServiceInstance
        //         {
        //             HostName = ListenIP,
        //             ServerUuid = service.Uuid,
        //             Port = service.Port.ToString(),
        //             Protocol = service.Protocol.ToString(),
        //             ConnectOrder = 0,
        //             Key = 0
        //         }
        //     }
        // });

        Services.Add(service);
    }

    public void Start()
    {
        foreach (BombdService service in Services) service.Start();

        GameServer.Start();
    }
}