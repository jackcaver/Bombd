using Bombd.Serialization;
using Bombd.Types.Network.Simulation;

namespace Bombd.Types.Network.Messages;

public struct NetMessageModnationPlayerUpdate : INetworkMessage
{
    public NetMessageType Type => NetMessageType.BulkPlayerStateUpdate;

    public ICollection<PlayerState> StateUpdates;

    public void Write(NetworkWriter writer)
    {
        writer.Write(StateUpdates.Count);
        foreach (PlayerState state in StateUpdates)
        {
            // writer.Write(state.NameUid);
            writer.Write(state.NetcodeUserId);
            writer.Write(state.PlayerConnectId);
            writer.Write(state.Away == 1);
            writer.Write(state.Mic == 1);
            writer.Write(state.HasEventVetoed);
            writer.Write(state.HasLeaderVetoed);
            writer.Write(state.IsConnecting);
            writer.Clear(0x3);
            writer.Write(state.KartHandlingDrift);
            writer.Write(state.KartSpeedAccel);
        }
    }
}