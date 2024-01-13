namespace Bombd.Types.Network.Room;

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