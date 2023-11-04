using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class SpectatorInfo : INetworkWritable, INetworkReadable
{
    public int LeaderNameUid;
    public int PostRaceServerTime;
    public int RaceEndServerTime;
    public int RaceLeaderLapNumber;

    public RaceState RaceState = RaceState.Invalid;
    public int TotalLapNumber;
    
    public void Read(NetworkReader reader)
    {
        RaceState = (RaceState)reader.ReadInt32();
        LeaderNameUid = reader.ReadInt32();
        RaceLeaderLapNumber = reader.ReadInt32();
        TotalLapNumber = reader.ReadInt32();
        RaceEndServerTime = reader.ReadInt32();
        PostRaceServerTime = reader.ReadInt32();
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write((int)RaceState);
        writer.Write(LeaderNameUid);
        writer.Write(RaceLeaderLapNumber);
        writer.Write(TotalLapNumber);
        writer.Write(RaceEndServerTime);
        writer.Write(PostRaceServerTime);
    }
}