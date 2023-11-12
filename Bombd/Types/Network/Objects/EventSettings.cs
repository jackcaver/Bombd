using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class EventSettings : INetworkReadable, INetworkWritable
{
    public int Version = 0xC;
    public readonly Platform Platform;
    
    // Shared
    public int CreationId = 288;
    public RaceType RaceType = RaceType.Pure;
    public int OwnerNetcodeUserId = -1;
    public bool Private;
    public int SeriesEventIndex = -1;
    public int MinHumans = 2;
    public int MaxHumans = 8;
    public bool AiEnabled = true;
    public int CareerEventIndex = -1;
    
    // Karting
    public int MaxPlayers = 8;
    public int NumHoard;
    
    // 2 = Leader Changed
    public int UpdateReason;
    public int LevelType;
    public int ScoreboardType;
    public bool TeamEnabled;
    public int StoryDifficulty;
    
    // Modnation
    public bool AutoReset = false;
    public bool IsRanked;
    public string StartNis = string.Empty;
    public SpeedClass KartSpeed = SpeedClass.Fastest;
    public int NumLaps = 3;
    public string TrackName = "Mod Circuit";

    public EventSettings(Platform platform)
    {
        Platform = platform;
        AutoReset = platform == Platform.ModNation;
    }
    
    public static EventSettings ReadVersioned(ArraySegment<byte> data, Platform platform)
    {
        var settings = new EventSettings(platform);
        using var reader = NetworkReaderPool.Get(data);
        settings.Read(reader);
        return settings;
    }
    
    public void Read(NetworkReader reader)
    {
        if (Platform == Platform.ModNation)
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
            IsRanked = reader.ReadInt32() == 1;
            Private = reader.ReadInt32() == 1;
            SeriesEventIndex = reader.ReadInt32();
            CareerEventIndex = reader.ReadInt32();
            reader.Offset += 4;

            MinHumans = reader.ReadInt32();
            MaxHumans = reader.ReadInt32();

            return;
        }

        Version = reader.ReadInt32();
        CreationId = reader.ReadInt32();
        MaxPlayers = reader.ReadInt32();
        RaceType = (RaceType)reader.ReadInt32();
        OwnerNetcodeUserId = reader.ReadInt32();
        Private = reader.ReadInt32() == 1;
        SeriesEventIndex = reader.ReadInt32();
        MinHumans = reader.ReadInt32();
        MaxHumans = reader.ReadInt32();
        NumHoard = reader.ReadInt32();
        AiEnabled = reader.ReadInt32() == 1;
        UpdateReason = reader.ReadInt32();
        CareerEventIndex = reader.ReadInt32();
        LevelType = reader.ReadInt32();
        ScoreboardType = reader.ReadInt32();
        TeamEnabled = reader.ReadInt32() == 1;
        StoryDifficulty = reader.ReadInt32();
    }

    public void Write(NetworkWriter writer)
    {
        if (Platform == Platform.ModNation)
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

            writer.Write(IsRanked ? 1 : 0); // 0x68
            writer.Write(Private ? 1 : 0); // 0x6c
            writer.Write(SeriesEventIndex);
            writer.Write(CareerEventIndex);
            writer.Write(0); // 0x78 // Top tracks
            writer.Write(MinHumans); // 0x7c 
            writer.Write(MaxHumans); // 0x80
            writer.Write(0); // 0x84 - Padding?
            writer.Write(0); // 0x88
        
            writer.Write(string.Empty, 0x40);
            writer.Write(StartNis, 0x80);

            return;
        }
        
        writer.Write(Version);
        writer.Write(CreationId);
        writer.Write(MaxPlayers);
        writer.Write((int)RaceType);
        writer.Write(OwnerNetcodeUserId);
        writer.Write(Private ? 1 : 0);
        writer.Write(SeriesEventIndex);
        writer.Write(MinHumans);
        writer.Write(MaxHumans);
        writer.Write(NumHoard);
        writer.Write(AiEnabled ? 1 : 0);
        writer.Write(UpdateReason);
        writer.Write(CareerEventIndex);
        writer.Write(LevelType);
        writer.Write(ScoreboardType);
        writer.Write(TeamEnabled ? 1 : 0);
        writer.Write(StoryDifficulty);
    }
    
}