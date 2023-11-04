using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class SpectatorInfo : INetworkWritable
{
    public const int LeaderboardSize = 8;
    public readonly int[] LeaderboardNameUids = new int[LeaderboardSize];
    public int PostRaceServerTime;
    public int RaceEndServerTime;
    public int RaceLeaderLapNumber;

    public int RaceState;
    public int TotalLapNumber;

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