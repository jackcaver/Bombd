using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Types.Network;
using Bombd.Types.Network.Messages;

namespace Bombd.Simulation;

public class GamePlayer
{
    public List<string> Guests = new();
    public int PlayerId;

    public GameRoom Room;
    public Action<ArraySegment<byte>, PacketType> Send;
    public int UserId;
    public string Username;

    public void OnNetworkMessage(NetMessageType type, ArraySegment<byte> data)
    {
        Room.GameSimulation.OnNetworkMessage(this, type, data);
    }

    public void SendReliableMessage(INetworkMessage message)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> packed = NetworkMessages.Pack(writer, message);
        Send(packed, PacketType.ReliableGameData);
    }
}