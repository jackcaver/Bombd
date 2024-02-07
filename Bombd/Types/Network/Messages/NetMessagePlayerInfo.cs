using Bombd.Serialization;
using Bombd.Types.Network.Simulation;

namespace Bombd.Types.Network.Messages;

public struct NetMessagePlayerInfo : INetworkWritable, INetworkReadable
{
    public List<PlayerInfo> Data;
    
    public void Read(NetworkReader reader)
    {
        int count = reader.ReadInt32();
        Data = new List<PlayerInfo>(count);
        for (int i = 0; i < count; ++i)
            Data.Add(reader.Read<PlayerInfo>());
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(Data.Count);
        foreach (PlayerInfo data in Data)
            writer.Write(data);
    }
}