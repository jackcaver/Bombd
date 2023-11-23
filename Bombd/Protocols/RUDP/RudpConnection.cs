using System.Net;
using Bombd.Core;
using Bombd.Extensions;
using Bombd.Helpers;
using Bombd.Logging;

namespace Bombd.Protocols.RUDP;

public class RudpConnection : ConnectionBase
{
    public const int MaxPayloadSize = 1024;
    public const int PacketTimeout = 30000;
    public const int ResendTime = 300;

    private readonly List<RudpAckRecord> _ackList = new(16);

    // The max size of a message is actually 0x800000, or something like 8mbs, but 1mb should probably be fine.
    private readonly byte[] _groupBuffer = new byte[1000000]; 
    private readonly RudpMessagePool _messagePool = new(32);
    private readonly List<RudpMessage> _sendBuffer = new(16);

    private readonly RudpServer _server;
    private int _groupOffset;

    private int _lastReceiveTime;
    private uint _localGamedataSequence;
    private ushort _localGroupNumber;

    private uint _localSequence;
    private uint _remoteGamedataSequence;
    private uint _remoteSequence;
    private int _secret;


    public RudpConnection(EndPoint endpoint, BombdService service, RudpServer server) : base(service, server)
    {
        _server = server;
        Endpoint = endpoint;
        State = ConnectionState.WaitingForHandshake;
    }

    public EndPoint Endpoint { get; }

    internal void OnData(ArraySegment<byte> data)
    {
        if (data.Count < 8)
        {
            Logger.LogInfo<RudpConnection>("Got packet with invalid length, dropping connection.");
            Disconnect();
            return;
        }

        if (State == ConnectionState.Disconnected) return;
        _lastReceiveTime = TimeHelper.LocalTime;

        int offset = data.Offset;
        byte[] buffer = data.Array!;
        var protocol = (PacketType)buffer[offset++];

        if (protocol == PacketType.Reset)
        {
            Logger.LogInfo<RudpConnection>("Received client disconnect. Disconnecting.");

            // The service only needs to know if *authenticated* clients disconnected.
            // So only if we passed the waiting for connection state.
            if (State > ConnectionState.WaitingForConnection) Service.OnDisconnected(this);

            State = ConnectionState.Disconnected;
            _server.connectionsToRemove.Add(Endpoint);

            return;
        }

        if (State == ConnectionState.WaitingForHandshake)
        {
            if (protocol != PacketType.Handshake)
            {
                Logger.LogInfo<RudpConnection>($"Expected handshake packet, instead got {protocol}. Disconnecting.");
                Disconnect();
                return;
            }

            offset += 1 + 2;
            offset += buffer.ReadUint32BE(offset, out uint sequence);
            offset += buffer.ReadInt32BE(offset, out int sessionId);
            offset += buffer.ReadInt32BE(offset, out int secretNum);
            offset += buffer.ReadUint32BE(offset, out uint gamedataSequence);

            _remoteSequence = sequence + 1;
            SessionId = sessionId;
            _secret = secretNum;
            _remoteGamedataSequence = gamedataSequence;

            RudpMessage msg = _messagePool.Get();
            msg.EncodeHandshake(sessionId, _secret);
            _sendBuffer.Add(msg);

            _ackList.Add(new RudpAckRecord { Protocol = protocol, SequenceNumber = sequence });

            State = ConnectionState.WaitingForConnection;
            return;
        }
        
        // This whole block is for handling acknowledgements and packet order.
        if (protocol != PacketType.UnreliableGameData)
        {
            buffer.ReadUint32BE(offset + 3, out uint sequence);
            if (protocol == PacketType.Ack)
            {
                var ackingProtocol = (PacketType)buffer[offset];
                int index = _sendBuffer.FindIndex(x => x.Sequence == sequence && x.Protocol == ackingProtocol);
                if (index >= 0) _sendBuffer.RemoveAt(index);
                return;
            }

            // We only need to acknowledge KeepAlive packets, so just stop here.
            if (protocol == PacketType.KeepAlive)
            {
                _ackList.Add(new RudpAckRecord { Protocol = protocol, SequenceNumber = sequence });
                return;
            }

            // Make sure Gamedata/Netcode messages are in-order, if they aren't, drop them so we don't end up
            // handling packets twice or in the wrong order.
            // keepalive doesn't follow a normal sequence number, it's just whatever the current timestamp is.
            if (State != ConnectionState.WaitingForHandshake)
            {
                if (protocol == PacketType.ReliableGameData)
                {
                    if (sequence < _remoteGamedataSequence)
                    {
                        _ackList.Add(new RudpAckRecord { Protocol = protocol, SequenceNumber = sequence });
                        // Logger.LogInfo<RudpConnection>($"Received gamedata packet out of order (Got {(uint)sequence}, Expected {(uint)_remoteSequenceNumber}). Dropping packet.");
                        return;
                    }
                    
                    _remoteGamedataSequence++;
                }
                else
                {
                    if (sequence < _remoteSequence)
                    {
                        _ackList.Add(new RudpAckRecord { Protocol = protocol, SequenceNumber = sequence });
                        // Logger.LogInfo<RudpConnection>("Received network message out of order. Dropping packet.");
                        return;
                    }

                    _remoteSequence++;
                }
            }
        }
        
        if (protocol == PacketType.Handshake)
        {
            Logger.LogInfo<RudpConnection>(
                "Connection attempted handshake while already connected. Disconnecting.");
            Disconnect();
            return;
        }

        if (IsAuthenticating)
        {
            if (protocol != PacketType.ReliableNetcodeData)
            {
                Logger.LogInfo<RudpConnection>(
                    "Expected netcode data during authentication. Disconnecting.");
                Disconnect();
                return;
            }

            offset += buffer.ReadBoolean(offset, out bool groupCompleteFlag);
            offset += buffer.ReadUint16BE(offset, out ushort groupId);
            offset += 4 + 4 + 4;

            int size = data.Count - (offset - data.Offset);
            Buffer.BlockCopy(buffer, offset, _groupBuffer, _groupOffset, size);
            _groupOffset += size;

            if (groupCompleteFlag)
            {
                var netcode = new ArraySegment<byte>(_groupBuffer, 0, _groupOffset);
                if (State == ConnectionState.WaitingForConnection) HandleStartConnect(netcode);
                else HandleTimeSync(netcode);
                _groupOffset = 0;
            }

            return;
        }

        // The only types of messages that we need to support now that we're connected
        // are just the gamedata messages, so either reliable or unreliable gamedata.
        if (protocol == PacketType.UnreliableGameData)
        {
            offset += 1 + 2;
            offset += buffer.ReadUint16BE(offset, out ushort payloadBytes);
            offset += 2;
            Service.OnData(this, new ArraySegment<byte>(buffer, offset, payloadBytes), protocol);
        }
        else if (protocol == PacketType.ReliableGameData)
        {
            offset += 1 + 1;

            byte groupCompleteFlags = buffer[offset++];
            bool isGroupComplete;
            if (Platform == Platform.Karting)
                isGroupComplete = (groupCompleteFlags & 0x80) != 0;
            else
                isGroupComplete = (groupCompleteFlags != 0);
            
            offset += 4;
            offset += buffer.ReadUint16BE(offset, out ushort groupId);
            offset += buffer.ReadUint16BE(offset, out ushort groupSizeBytes);
            offset += 2;
            offset += buffer.ReadUint16BE(offset, out ushort payloadBytes);

            Buffer.BlockCopy(buffer, offset, _groupBuffer, _groupOffset, payloadBytes);
            _groupOffset += payloadBytes;

            if (isGroupComplete)
            {
                Service.OnData(this, new ArraySegment<byte>(_groupBuffer, 0, _groupOffset), protocol);
                _groupOffset = 0;
            }
        }
        else
            Logger.LogInfo<RudpConnection>($"Unsupported protocol: {protocol}");
    }

    internal void Update()
    {
        int time = TimeHelper.LocalTime;

        if (time >= _lastReceiveTime + PacketTimeout)
        {
            Logger.LogDebug<RudpConnection>("Connection timed out. Disconnecting.");
            Disconnect();
            return;
        }

        foreach (RudpMessage message in _sendBuffer)
        {
            if (time - message.Timestamp < ResendTime) continue;
            
            message.Timestamp = time;
            _server.Send(this, message.GetArraySegment());
        }

        if (_ackList.Count > 0)
        {
            RudpMessage ackMessage = _messagePool.Get();
            foreach (RudpAckRecord ack in _ackList)
            {
                ackMessage.EncodeAck(ack.Protocol, ack.SequenceNumber);
                _server.Send(this, ackMessage.GetArraySegment());
                ackMessage.Reset();
            }

            _messagePool.Return(ackMessage);

            _ackList.Clear();
        }
    }

    public override void Send(ArraySegment<byte> data, PacketType protocol)
    {
        switch (protocol)
        {
            case PacketType.ReliableGameData:
            {
                SendReliable(PacketType.ReliableGameData, data);
                break;
            }
            case PacketType.ReliableNetcodeData:
            {
                SendReliable(PacketType.ReliableNetcodeData, data);
                break;
            }
            case PacketType.UnreliableGameData:
            {
                RudpMessage msg = _messagePool.Get();
                msg.EncodeUnreliableGamedata(data);
                _server.Send(this, msg.GetArraySegment());
                _messagePool.Return(msg);
                break;
            }
            case PacketType.VoipData:
            {
                RudpMessage msg = _messagePool.Get();
                msg.EncodeVoipData((uint)TimeHelper.LocalTime, data);
                _server.Send(this, msg.GetArraySegment());
                _messagePool.Return(msg);
                break;
            }
        }
    }

    public override void Disconnect()
    {
        if (State == ConnectionState.Disconnected) return;

        RudpMessage msg = _messagePool.Get();
        msg.EncodeReset(_localSequence++, _secret);
        _sendBuffer.Add(msg);

        if (State > ConnectionState.WaitingForConnection) Service.OnDisconnected(this);

        State = ConnectionState.Disconnected;

        _server.connectionsToRemove.Add(Endpoint);
    }

    private void SendReliable(PacketType protocol, ArraySegment<byte> data)
    {
        int len = data.Count;

        int count;
        if (len <= MaxPayloadSize) count = 1;
        else count = (len + MaxPayloadSize - 1) / MaxPayloadSize;

        ushort groupSize = (ushort)len;
        ushort groupNumber = _localGroupNumber++;

        int offset = 0;
        for (int i = 0; i < count; ++i)
        {
            int size = len > MaxPayloadSize ? MaxPayloadSize : len;
            RudpMessage msg = _messagePool.Get();
            var slice = new ArraySegment<byte>(data.Array!, data.Offset + offset, size);

            offset += size;
            len -= size;

            if (protocol == PacketType.ReliableNetcodeData)
                msg.EncodeNetcode(groupNumber, _localSequence++, slice, len == 0);
            else
                msg.EncodeGamedata(groupNumber, _localGamedataSequence++, groupSize, slice, len == 0);

            _sendBuffer.Add(msg);
        }
    }
}