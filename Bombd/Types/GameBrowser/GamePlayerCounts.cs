using Bombd.Serialization;

namespace Bombd.Types.GameBrowser;

public class GamePlayerCounts : INetworkReadable, INetworkWritable
{
    public string AttributeName = string.Empty;
    public Dictionary<int, int> Data = new();

    public void Read(NetworkReader reader)
    {
        AttributeName = reader.ReadString();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; ++i)
            Data[reader.ReadInt32()] = reader.ReadInt32();
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(AttributeName);
        writer.Write(Data.Count);
        foreach (var result in Data)
        {
            writer.Write(result.Key);
            writer.Write(result.Value);
        }
    }
}