using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class PlayerConfig : INetworkReadable, INetworkWritable
{
    public int Type;
    public int NetcodeUserId;
    public int CharCreationId;
    public int KartCreationId;
    public string UidName = string.Empty;
    public string Username = string.Empty;
    public ArraySegment<byte> KartDataBlob = ArraySegment<byte>.Empty;
    public ArraySegment<byte> CharDataBlob = ArraySegment<byte>.Empty;
    
    public void Read(NetworkReader reader)
    {
        Type = reader.ReadInt32();
        NetcodeUserId = reader.ReadInt32();
        CharCreationId = reader.ReadInt32();
        KartCreationId = reader.ReadInt32();
        int kartDataBlobSize = reader.ReadInt32();
        int charDataBlobSize = reader.ReadInt32();
        UidName = reader.ReadString();
        Username = reader.ReadString();
        reader.Offset += 4;
        KartDataBlob = reader.ReadBytes(kartDataBlobSize);
        reader.Offset += 4;
        CharDataBlob = reader.ReadBytes(charDataBlobSize);
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write(Type);
        writer.Write(NetcodeUserId);
        writer.Write(CharCreationId);
        writer.Write(KartCreationId);
        writer.Write(KartDataBlob.Count);
        writer.Write(CharDataBlob.Count);
        writer.Write(UidName);
        writer.Write(Username);
        writer.Write(KartDataBlob.Count);
        writer.Write(KartDataBlob);
        writer.Write(CharDataBlob.Count);
        writer.Write(CharDataBlob);
    }
}