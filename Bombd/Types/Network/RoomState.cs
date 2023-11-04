namespace Bombd.Types.Network;

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