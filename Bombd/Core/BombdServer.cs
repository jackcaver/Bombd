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


    private static BombdServer _instance;
    public static BombdServer Instance => _instance;
    
    public BombdServer()
    {
        if (_instance != null)
        {
            throw new Exception("Can't initialize multiple Bombd servers!");
        }
        
        _instance = this;

        // I don't believe the gameserver ever actually gets directly sent
        // via directory, plus GameManager and GameBrowser are going to need
        // access to it anyway.
        GameServer = new GameServer();
    }
    
    public string ClusterUuid { get; } = CryptoHelper.GetRandomUUID();
    public List<BombdService> Services { get; } = new();
    public GameServer GameServer { get; }
    public RoomManager RoomManager { get; } = new();
    public SessionManager SessionManager { get; } = new();

    public T GetService<T>() where T : BombdService => (T)_serviceCache[typeof(T)];
    
    public void AddService<T>() where T : BombdService, new()
    {
        var service = new T();
        _serviceCache.Add(typeof(T), service);
        Services.Add(service);
    }

    public void Start()
    {
        foreach (BombdService service in Services) service.Start();

        GameServer.Start();
    }
}