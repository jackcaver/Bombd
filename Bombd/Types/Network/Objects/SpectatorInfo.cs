using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class SpectatorInfo : INetworkWritable, INetworkReadable
{
    public readonly Platform Platform;
    
    public int LeaderNameUid
    {
        get => LeaderboardNameUids[0];
        set => LeaderboardNameUids[0] = value;
    }
    
    public readonly int[] LeaderboardNameUids = new int[8];
    public int PostRaceServerTime;
    public int RaceEndServerTime;
    public int RaceLeaderLapNumber;

    public RaceState RaceState = RaceState.Invalid;
    public int TotalLapNumber;

    public SpectatorInfo(Platform platform)
    {
        Platform = platform;
    }

    public static SpectatorInfo ReadVersioned(ArraySegment<byte> data, Platform platform)
    {
        var info = new SpectatorInfo(platform);
        using var reader = NetworkReaderPool.Get(data);
        info.Read(reader);
        return info;
    }
    
    public void Read(NetworkReader reader)
    {
        RaceState = (RaceState)reader.ReadInt32();
        if (Platform == Platform.ModNation) LeaderNameUid = reader.ReadInt32();
        RaceLeaderLapNumber = reader.ReadInt32();
        TotalLapNumber = reader.ReadInt32();
        RaceEndServerTime = reader.ReadInt32();
        PostRaceServerTime = reader.ReadInt32();
        if (Platform == Platform.Karting)
        {
            for (int i = 0; i < 8; ++i)
                LeaderboardNameUids[i] = reader.ReadInt32();
        }
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write((int)RaceState);
        if (Platform == Platform.ModNation) writer.Write(LeaderNameUid);
        writer.Write(RaceLeaderLapNumber);
        writer.Write(TotalLapNumber);
        writer.Write(RaceEndServerTime);
        writer.Write(PostRaceServerTime);
        if (Platform == Platform.Karting)
        {
            for (int i = 0; i < 8; ++i)
                writer.Write(LeaderboardNameUids[i]);
        }
    }
}