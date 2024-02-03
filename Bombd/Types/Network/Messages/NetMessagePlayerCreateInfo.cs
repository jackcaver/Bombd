using Bombd.Serialization;
using Bombd.Types.Network.Simulation;

namespace Bombd.Types.Network.Messages;

public struct NetMessagePlayerCreateInfo : INetworkMessage, INetworkReadable
{
    public NetMessageType Type => NetMessageType.PlayerCreateInfo;

    public List<PlayerInfo> Data;

    public void Read(NetworkReader reader)
    {
        Data = new List<PlayerInfo>();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; ++i)
            Data.Add(reader.Read<PlayerInfo>());
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(Data.Count);
        foreach (var data in Data)
            writer.Write(data);
    }
}