using Bombd.Helpers;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Types.Network;
using Bombd.Types.Network.Messages;
using Bombd.Types.Network.Room;

namespace Bombd.Simulation;

public class GamePlayer
{
    public readonly Platform Platform;
    public readonly GameRoom Room;
    public Action<ArraySegment<byte>, PacketType> Send = (_, _) => { };
    public Action Disconnect = () => { };
    
    public readonly int PlayerId;
    public readonly int UserId;
    public readonly string Username;
    
    public PlayerInfo? Info;
    public readonly PlayerState State = new();
    
    // Convenience accessor for the primary guest
    public GameGuest? Guest => Guests.Count > 0 ? Guests[0] : null;

    // Karting has multiple guests in the pod so we have to keep it in a list,
    // even though racing and ModNation only have a single guest
    public readonly List<GameGuest> Guests = new();

    public LeaveReason LeaveReason = LeaveReason.Generic;
    public bool IsSpectator;
    public bool HasSentRaceResults;
    public bool ListeningForGameEvents;

    public GamePlayer(GameRoom room, string username, int userId, int playerId)
    {
        Platform = room.Platform;
        
        Room = room;
        Username = username;
        UserId = userId;
        PlayerId = playerId;
        
        State.NetcodeUserId = userId;
    }
    
    public GameGuest? GetGuestByName(string username)
    {
        return Guests.FirstOrDefault(x => x.GuestName == username);
    }
    
    public void OnNetworkMessage(NetMessageType type, uint sender, ArraySegment<byte> data)
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