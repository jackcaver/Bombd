using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class RaceSettings : INetworkWritable, INetworkReadable
{
    public bool AiEnabled;
    public int CreationId;
    public string Description = string.Empty;
    public int KartSpeed;
    public int MaxHumans;
    public int MaxPlayers;
    public int MinHumans;
    public int NumLaps;
    public int OwnerNetcodeUserId;
    public bool Private;
    public int RaceType;
    public string TrackName = string.Empty;

    public void Read(NetworkReader reader)
    {
        TrackName = reader.ReadString(0x40);
        CreationId = reader.ReadInt32();
        MaxPlayers = reader.ReadInt32();
        reader.Offset += 4;
        NumLaps = reader.ReadInt32();
        KartSpeed = reader.ReadInt32();
        RaceType = reader.ReadInt32();
        reader.Offset += 4;
        AiEnabled = reader.ReadInt32() == 1;
        reader.Offset += 4;
        OwnerNetcodeUserId = reader.ReadInt32();
        reader.Offset += 4;
        reader.Offset += 4;
        reader.Offset += 4;
        reader.Offset += 4;
        reader.Offset += 4;

        MinHumans = reader.ReadInt32();
        MaxHumans = reader.ReadInt32();
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write(TrackName, 0x40);
        writer.Write(CreationId);
        writer.Write(MaxPlayers); // - PlayerCount or something?
        writer.Write(1); // - Unknown
        writer.Write(NumLaps);
        writer.Write(KartSpeed);
        writer.Write(RaceType);
        writer.Write(0); // - Unknown
        writer.Write(AiEnabled ? 1 : 0);
        writer.Write(1); // - Unknown
        writer.Write(OwnerNetcodeUserId);

        writer.Write(0); // 0x68
        writer.Write(Private ? 1 : 0); // 0x6c
        writer.Write(-1); // 0x70 - Series Event Index?
        writer.Write(-1); // 0x74 - Career Event Index?
        writer.Write(0); // 0x78
        writer.Write(2); // 0x7c - MinHumans?
        writer.Write(8); // 0x80 - MaxHumans?
        writer.Write(0); // 0x84 - Padding?
        writer.Write(0); // 0x88

        writer.Write(string.Empty, 0x40);
        writer.Write(Description, 0x80);
    }
}