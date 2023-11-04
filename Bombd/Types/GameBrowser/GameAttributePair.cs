using Bombd.Serialization;

namespace Bombd.Types.GameBrowser;

public class GameAttributePair : INetworkReadable, INetworkWritable
{
    public string Key = string.Empty;
    public string Value = string.Empty;
    
    public void Read(NetworkReader reader)
    {
        Key = reader.ReadString(0x20);
        Value = reader.ReadString(0x20);
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write(Key, 0x20);
        writer.Write(Value, 0x20);
    }
}