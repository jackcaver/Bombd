using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class EventSettings : INetworkWritable, INetworkReadable
{
    public bool AiEnabled = true;
    public int CreationId = 288;
    public string Description = string.Empty;
    public SpeedClass KartSpeed = SpeedClass.Fastest;
    public int MaxPlayers;
    public int MinPlayers;
    public int NumLaps = 3;
    public int OwnerNetcodeUserId = -1;
    public bool Private;
    public int SeriesEventIndex = -1;
    public int CareerEventIndex = -1;
    public RaceType RaceType = RaceType.Pure;
    public string TrackName = "Mod Circuit";

    public void Read(NetworkReader reader)
    {
        TrackName = reader.ReadString(0x40);
        CreationId = reader.ReadInt32();
        reader.Offset += 4;
        reader.Offset += 4;
        NumLaps = reader.ReadInt32();
        KartSpeed = (SpeedClass)reader.ReadInt32();
        RaceType = (RaceType)reader.ReadInt32();
        reader.Offset += 4;
        AiEnabled = reader.ReadInt32() == 1;
        reader.Offset += 4;
        OwnerNetcodeUserId = reader.ReadInt32();
        reader.Offset += 4;
        Private = reader.ReadInt32() == 1;
        SeriesEventIndex = reader.ReadInt32();
        CareerEventIndex = reader.ReadInt32();
        reader.Offset += 4;

        MinPlayers = reader.ReadInt32();
        MaxPlayers = reader.ReadInt32();
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write(TrackName, 0x40);
        writer.Write(CreationId);
        writer.Write(1); // - PlayerCount or something?
        writer.Write(1); // - Unknown
        writer.Write(NumLaps);
        writer.Write((int)KartSpeed);
        writer.Write((int)RaceType);
        writer.Write(0); // - Unknown
        writer.Write(AiEnabled ? 1 : 0);
        writer.Write(1); // - Unknown
        writer.Write(OwnerNetcodeUserId);

        writer.Write(0); // 0x68
        writer.Write(Private ? 1 : 0); // 0x6c
        writer.Write(SeriesEventIndex);
        writer.Write(CareerEventIndex);
        writer.Write(0); // 0x78
        writer.Write(MinPlayers); // 0x7c 
        writer.Write(MaxPlayers); // 0x80
        writer.Write(0); // 0x84 - Padding?
        writer.Write(0); // 0x88
        
        writer.Write(string.Empty, 0x40);
        writer.Write(Description, 0x80);
    }
}