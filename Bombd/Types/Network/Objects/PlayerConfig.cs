using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class PlayerConfig : INetworkReadable, INetworkWritable
{
    public readonly Platform Platform;
    
    public int Type;
    public int NetcodeUserId;
    public int CharCreationId;
    public int KartCreationId;
    public string UidName = string.Empty;
    public string Username = string.Empty;
    public ArraySegment<byte> KartDataBlob = ArraySegment<byte>.Empty;
    public ArraySegment<byte> CharDataBlob = ArraySegment<byte>.Empty;

    public PlayerConfig(Platform platform) => Platform = platform;
    
    public static PlayerConfig ReadVersioned(ArraySegment<byte> data, Platform platform)
    {
        var config = new PlayerConfig(platform);
        using var reader = NetworkReaderPool.Get(data);
        config.Read(reader);
        return config;
    }
    
    public void Read(NetworkReader reader)
    {
        Type = reader.ReadInt32();
        if (Platform == Platform.Karting)
        {
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
        else
        {
            UidName = reader.ReadString(0x80);
            Username = reader.ReadString(0x20);
            NetcodeUserId = reader.ReadInt32();
            int kartDataBlobSize = reader.ReadInt32();
            int charDataBlobSize = reader.ReadInt32();
            CharCreationId = reader.ReadInt32();
            KartCreationId = reader.ReadInt32();
            CharDataBlob = reader.ReadBytes(charDataBlobSize);
            KartDataBlob = reader.ReadBytes(kartDataBlobSize);
        }
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write(Type);
        if (Platform == Platform.Karting)
        {
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
        else
        {
            writer.Write(UidName, 0x80);
            writer.Write(Username, 0x20);
            writer.Write(NetcodeUserId);
            writer.Write(KartDataBlob.Count);
            writer.Write(CharDataBlob.Count);
            writer.Write(CharCreationId);
            writer.Write(KartCreationId);
            writer.Write(CharDataBlob);
            writer.Write(KartDataBlob);
        }
    }
}