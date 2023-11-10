namespace Bombd.Types.Network;

public class PlayerStateFlags
{
    public const int None = 0;
    public const int GameRoomReady = (1 << 0);
    public const int DownloadedTracks = (1 << 1);
    public const int ReadyForNis = (1 << 2);
    public const int ReadyForEvent = (1 << 3);
}