using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class GameroomState : INetworkWritable
{
    public enum RoomState
    {
        None,
        WaitingMinPlayers,
        Ready,
        DownloadingTracks,
        CountingDown,
        CountingDownPaused,
        RaceInProgress
    }

    public bool HasEventVetoOccured;
    public bool IsLeaderVetoAvailable;
    public int LoadEventTime;
    public float LockedForRacerJoinsValue;
    public float LockedTimerValue;

    public RoomState State = RoomState.None;

    public void Write(NetworkWriter writer)
    {
        writer.Write((int)State);
        writer.Write(LoadEventTime);
        writer.Write(LockedForRacerJoinsValue);
        writer.Write(LockedTimerValue);
        writer.Write(IsLeaderVetoAvailable);
        writer.Write(HasEventVetoOccured);
        writer.Clear(0x2);
    }
}