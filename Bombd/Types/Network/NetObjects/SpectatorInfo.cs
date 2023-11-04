using Bombd.Serialization;

namespace Bombd.Types.Network.NetObjects;

public class SpectatorInfo : INetworkWritable
{
    public const int LeaderboardSize = 8;
    
    public int RaceState;
    public readonly int[] LeaderboardNameUids = new int[LeaderboardSize];
    public int RaceLeaderLapNumber;
    public int TotalLapNumber;
    public int RaceEndServerTime;
    public int PostRaceServerTime;
    
    public void Write(NetworkWriter writer)
    {
        // writer.Write(RaceState);
        // foreach (var uid in LeaderboardNameUids)
        //     writer.Write(uid);
        // writer.Write(RaceLeaderLapNumber);
        // writer.Write(TotalLapNumber);
        // writer.Write(RaceEndServerTime);
        // writer.Write(PostRaceServerTime);
        
        writer.Clear(24);
    }
}