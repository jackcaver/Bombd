using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Types.Network.Messages;

namespace Bombd.Types.Network;

public class GamePlayer
{
    public List<string> Guests = new();
    public int PlayerId;
    public int UserId;
    public string Username;
    
    public GameRoom Room;
    public Action<ArraySegment<byte>, PacketType> Send;

    public void OnNetworkMessage(NetMessageType type, ArraySegment<byte> data)
    {
        Room.Simulation.OnNetworkMessage(this, type, data);
    }
    
    public void SendReliableMessage(INetworkMessage message)
    {
        using var writer = NetworkWriterPool.Get();
        var packed = NetworkMessages.Pack(writer, message);
        Send(packed, PacketType.ReliableGameData);
    }
}