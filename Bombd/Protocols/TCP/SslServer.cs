using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Bombd.Core;
using Bombd.Logging;

namespace Bombd.Protocols.TCP;

public class SslServer : IServer
{
    private readonly BombdService _service;
    private Socket? _socket;
    
    public SslServer(
        string address,
        int port,
        X509Certificate certificate,
        BombdService service
    )
    {
        Certificate = certificate;
        Endpoint = new IPEndPoint(IPAddress.Parse(address), port);
        _service = service;
    }

    public X509Certificate Certificate { get; }
    public Dictionary<int, SslConnection> Connections { get; } = new();
    public string Name => "TCP";
    public EndPoint Endpoint { get; private set; }
    public bool IsActive => _socket != null;

    public void Start()
    {
        if (IsActive)
        {
            Logger.LogWarning<SslServer>("Server has already been started!");
            return;
        }

        _socket = new Socket(Endpoint.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _socket.Bind(Endpoint);
        _socket.Listen(128);

        Endpoint = _socket.LocalEndPoint!;

        Task.Run(async () => await Block());
    }

    public void Stop()
    {
        if (!IsActive) return;

        _socket.Close();
        _socket.Dispose();
        _socket = null;

        foreach (SslConnection session in Connections.Values) session.Disconnect();

        Connections.Clear();
    }

    public void Tick()
    {
        _service.OnTick();
    }
    
    private async Task Block()
    {
        while (IsActive)
        {
            try
            {
                Socket client = await _socket.AcceptAsync();
                var session = new SslConnection(_service, this);
                session.Connect(client);
            }
            catch (Exception e)
            {
                Logger.LogError<SslServer>("An error occurred while accepting a connection");
                Logger.LogError<SslServer>(e.ToString());
            }            
        }
    }
}