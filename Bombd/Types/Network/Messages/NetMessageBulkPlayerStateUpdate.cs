using Bombd.Serialization;

namespace Bombd.Types.Network.Messages;

public struct NetMessageBulkPlayerStateUpdate : INetworkMessage
{
    public NetMessageType Type => NetMessageType.BulkPlayerStateUpdate;

    public ICollection<PlayerState> StateUpdates;
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(StateUpdates.Count);
        foreach (var state in StateUpdates)
        {
            writer.Write(state.NameUid);
            writer.Write(state.PlayerConnectId);
            writer.Write(state.KartId);
            writer.Write(state.CharacterId);
            writer.Write(state.Away);
            writer.Write(state.Mic);
        }
    }
}