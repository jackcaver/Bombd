using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Bombd.Core;
using Bombd.Logging;

namespace Bombd.Protocols.RUDP;

public class RudpServer : IServer
{
    public const long Timestep = 66;
    private readonly byte[] _recv = new byte[1040];

    private readonly BombdService _service;

    private readonly Stopwatch _watch = new();
    public readonly Dictionary<EndPoint, RudpConnection> Connections = new();

    internal readonly HashSet<EndPoint> connectionsToRemove = new();


    private long _accumulatedTime;
    private EndPoint _clientEndpoint = new IPEndPoint(IPAddress.Any, 0);
    private EndPoint _endpoint;
    private long _lastTime;
    private Socket? _socket;

    public RudpServer(
        string address,
        int port,
        BombdService service)
    {
        Endpoint = new IPEndPoint(IPAddress.Parse(address), port);
        _service = service;
    }

    public string Name => "RUDP";
    public bool IsActive => _socket != null;
    public EndPoint Endpoint { get; }

    public void Start()
    {
        if (_socket != null)
        {
            Logger.LogWarning<RudpServer>("Server has already been started!");
            return;
        }

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _socket.Bind(Endpoint);
        _socket.Blocking = false;
        _socket.ReceiveBufferSize = 1024 * 1024 * 7;
        _socket.SendBufferSize = 1024 * 1024 * 7;

        _watch.Start();
        _lastTime = _watch.ElapsedMilliseconds;
    }

    public void Stop()
    {
        // TODO: Implement this???
    }

    private bool ReceiveFrom(out ArraySegment<byte> segment)
    {
        segment = default;

        try
        {
            if (!_socket.Poll(0, SelectMode.SelectRead)) return false;

            int size = _socket.ReceiveFrom(_recv, 0, _recv.Length, SocketFlags.None,
                ref _clientEndpoint);

            segment = new ArraySegment<byte>(_recv, 0, size);

            return true;
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode != SocketError.WouldBlock) Logger.LogInfo<RudpServer>($"ReceiveFrom failed: {ex}");
        }

        return false;
    }

    public void Send(RudpConnection connection, ArraySegment<byte> data)
    {
        try
        {
            if (!_socket.Poll(0, SelectMode.SelectWrite)) return;
            _socket.SendTo(data.Array, data.Offset, data.Count, SocketFlags.None, connection.Endpoint);
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode != SocketError.WouldBlock) Logger.LogInfo<RudpServer>($"SendTo failed: {ex}");
        }
    }

    private void HandleData(ArraySegment<byte> segment)
    {
        if (Connections.TryGetValue(_clientEndpoint, out RudpConnection? connection))
        {
            connection.OnData(segment);
            return;
        }

        connection = new RudpConnection(_clientEndpoint, _service, this);
        Connections.Add(_clientEndpoint, connection);
        connection.OnData(segment);
    }

    private void Tick()
    {
        while (ReceiveFrom(out ArraySegment<byte> segment)) HandleData(segment);

        _service.OnTick();

        foreach (RudpConnection connection in Connections.Values) connection.Update();

        foreach (EndPoint client in connectionsToRemove) Connections.Remove(client);

        connectionsToRemove.Clear();
    }

    public void Update()
    {
        long time = _watch.ElapsedMilliseconds;
        _accumulatedTime += time - _lastTime;
        while (_accumulatedTime > Timestep)
        {
            Tick();
            _accumulatedTime -= Timestep;
        }

        _lastTime = time;
    }
}