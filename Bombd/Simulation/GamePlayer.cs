using Bombd.Helpers;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Types.Network;
using Bombd.Types.Network.Messages;

namespace Bombd.Simulation;

public class GamePlayer
{
    public required Platform Platform;
    public GameRoom Room;
    public Action<ArraySegment<byte>, PacketType> Send;
    public Action Disconnect;
    
    public int PlayerId;
    public int UserId;
    public string Username;
    public List<string> Guests = new();
    
    public bool IsFakePlayer;
    public bool ListeningForGameEvents;
    
    public void OnNetworkMessage(NetMessageType type, int sender, ArraySegment<byte> data)
    {
        Room.Simulation.OnNetworkMessage(this, sender, type, data);
    }

    public void SendReliableMessage(INetworkMessage message)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        ArraySegment<byte> packed = NetworkMessages.Pack(writer, message);
        Send(packed, PacketType.ReliableGameData);
    }
}