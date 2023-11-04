using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Timers;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Logging;
using Timer = System.Timers.Timer;

namespace Bombd.Protocols.TCP;

public class SslConnection : ConnectionBase
{
    private const int MaxMessageSize = 8192;
    private const int KeepAliveFrequency = 3000;

    // u32 MsgLength
    // char Md5Digest[16]
    // char Protocol
    // char Source
    // short Destination
    private const int MessageHeaderSize = 24;
    private readonly byte[] _recv = new byte[MaxMessageSize];

    private readonly byte[] _send = new byte[MaxMessageSize];

    private readonly object _sendLock = new();
    private Timer? _keepAliveTimer;

    private SslStream _sslStream;

    public SslConnection(BombdService service, SslServer server) : base(service, server)
    {
        Id = CryptoHelper.GetRandomSecret();
        Server = server;
        State = ConnectionState.WaitingForConnection;
    }

    public int Id { get; }
    public bool IsConnected { get; private set; }
    public SslServer Server { get; }
    public Socket Socket { get; private set; }

    public void Connect(Socket socket)
    {
        Socket = socket;

        // Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        // Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 3);

        IsConnected = true;

        try
        {
            _sslStream = new SslStream(new NetworkStream(Socket, false), false);
            _sslStream.AuthenticateAsServer(Server.Certificate, false, SslProtocols.Ssl3 | SslProtocols.Tls, false);

            _keepAliveTimer = new Timer(KeepAliveFrequency);
            _keepAliveTimer.Elapsed += OnKeepAliveTimerElapsed;
            _keepAliveTimer.AutoReset = false;
            _keepAliveTimer.Enabled = true;

            StartReceive();
        }
        catch (Exception ex)
        {
            Logger.LogError<SslConnection>("Handshake failed. Disconnecting.");
            Disconnect();
        }
    }

    public override void Disconnect()
    {
        if (!IsConnected) return;

        try
        {
            _keepAliveTimer?.Dispose();

            try
            {
                _sslStream.ShutdownAsync().Wait();
            }
            catch (Exception)
            {
                Logger.LogError<SslConnection>("An error occurred while shutting down SSL stream.");
            }

            _sslStream.Dispose();

            try
            {
                Socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException ex)
            {
                Logger.LogError<SslConnection>("An error occurred while shutting down socket.");
            }

            Socket.Close();
            Socket.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // log later ig
        }

        Server.Connections.Remove(Id);
        if (State > ConnectionState.WaitingForConnection) Service.OnDisconnected(this);
        IsConnected = false;
    }

    private void OnKeepAliveTimerElapsed(object? source, ElapsedEventArgs e)
    {
        Send(ArraySegment<byte>.Empty, PacketType.KeepAlive);
    }

    public override void Send(ArraySegment<byte> data, PacketType type)
    {
        if (!IsConnected) return;
        int messageSize = MessageHeaderSize + data.Count;
        lock (_sendLock)
        {
            _send[messageSize++] = 0;

            int packetSize = messageSize - 4;
            _send[0] = (byte)((packetSize >> 24) & 0xff);
            _send[1] = (byte)((packetSize >> 16) & 0xff);
            _send[2] = (byte)((packetSize >> 8) & 0xff);
            _send[3] = (byte)(packetSize & 0xff);

            _send[20] = (byte)type;
            _send[21] = 0xFE;
            _send[22] = 0xFF;
            _send[23] = 0xFF;

            Buffer.BlockCopy(data.Array!, data.Offset, _send, MessageHeaderSize, data.Count);
            try
            {
                int offset = 0;
                do
                {
                    int size = Math.Min(1024, messageSize - offset);
                    _sslStream.Write(_send, offset, size);
                    offset += size;
                } while (offset < messageSize);
            }
            catch (Exception)
            {
                Logger.LogWarning<SslConnection>("An error occurred during send. Closing connection.");
                Disconnect();
            }
        }
    }

    private async void StartReceive()
    {
        if (!IsConnected) return;

        int payloadSize;
        PacketType type;
        try
        {
            int len = await _sslStream.ReadAsync(_recv, 0, MessageHeaderSize);
            if (len != MessageHeaderSize)
            {
                Disconnect();
                return;
            }

            payloadSize = ((_recv[0] << 24) | (_recv[1] << 16) | (_recv[2] << 8) | _recv[3]) -
                          (MessageHeaderSize - 4);
            if (payloadSize < 0 || payloadSize > MaxMessageSize)
            {
                Disconnect();
                return;
            }

            type = (PacketType)_recv[20];
            if (payloadSize != 0)
            {
                int offset = 0;
                do
                {
                    len = await _sslStream.ReadAsync(_recv, offset, payloadSize - offset);
                    if (len == 0)
                    {
                        Disconnect();
                        return;
                    }

                    offset += len;
                } while (offset < payloadSize);
            }
        }
        catch (Exception)
        {
            Disconnect();
            return;
        }

        if (type == PacketType.KeepAlive) _keepAliveTimer!.Start();
        else
        {
            var data = new ArraySegment<byte>(_recv, 0, payloadSize);
            if (State == ConnectionState.WaitingForConnection) HandleStartConnect(data);
            else if (State == ConnectionState.WaitingForTimeSync) HandleTimeSync(data);
            else Service.OnData(this, data, type);
        }

        StartReceive();
    }
}