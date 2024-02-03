using Bombd.Serialization;
using Bombd.Types.Network.Simulation;

namespace Bombd.Types.Network.Messages;

public struct NetMessageKartingPlayerUpdate : INetworkMessage, INetworkReadable
{
    public NetMessageType Type => NetMessageType.BulkPlayerStateUpdate;
    public ICollection<PlayerState> StateUpdates;
    
    public void Read(NetworkReader reader)
    {
        StateUpdates = new List<PlayerState>();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; ++i)
        {
            var state = new PlayerState
            {
                NameUid = reader.ReadUInt32(),
                Away = (reader.ReadBool() ? 1 : 0),
                Mic = (reader.ReadBool() ? 1 : 0),
                HasEventVetoed = reader.ReadBool(),
                HasLeaderVetoed = reader.ReadBool(),
                IsConnecting = reader.ReadBool(),
                KartHandlingDrift = reader.ReadSingle(),
                KartSpeedAccel = reader.ReadSingle(),
                KartId = reader.ReadInt32(),
                CharacterId = reader.ReadInt32()
            };
            
            StateUpdates.Add(state);
        }
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(StateUpdates.Count);
        foreach (var state in StateUpdates)
        {
            writer.Write(state.NameUid);
            writer.Write(state.Away != 0);
            writer.Write(state.Mic != 0);
            writer.Write(state.HasEventVetoed);
            writer.Write(state.HasLeaderVetoed);
            writer.Write(state.IsConnecting);
            writer.Write(state.KartHandlingDrift);
            writer.Write(state.KartSpeedAccel);
            writer.Write(state.KartId);
            writer.Write(state.CharacterId);
        }
    }
}