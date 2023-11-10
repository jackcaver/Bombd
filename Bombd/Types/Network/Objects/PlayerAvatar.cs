using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class PlayerAvatar : INetworkReadable, INetworkWritable
{
    public string OwnerName = string.Empty;
    public ArraySegment<byte> SmileyDataBlob = ArraySegment<byte>.Empty;
    public ArraySegment<byte> FrownyDataBlob = ArraySegment<byte>.Empty;
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(OwnerName);
        
        writer.Write(SmileyDataBlob.Count);
        writer.Write(SmileyDataBlob.Count);
        writer.Write(SmileyDataBlob);
        
        writer.Write(FrownyDataBlob.Count);
        writer.Write(FrownyDataBlob.Count);
        writer.Write(FrownyDataBlob);
    }
    
    public void Read(NetworkReader reader)
    {
        OwnerName = reader.ReadString();
        reader.Offset += 4;
        SmileyDataBlob = reader.ReadBytes(reader.ReadInt32());
        reader.Offset += 4;
        FrownyDataBlob = reader.ReadBytes(reader.ReadInt32());
    }
}