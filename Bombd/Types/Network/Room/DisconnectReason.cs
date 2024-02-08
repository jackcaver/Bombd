namespace Bombd.Types.Network.Room;

public static class DisconnectReason
{
    public const string Generic = "disconnected";
    public const string UserRequested = "userInitiatedLeave";
    
    public const string LeaderKickRequest = "leaderKickRequest";
    public const string TrackDownloadFailed = "trackDownloadFailed";
    public const string TrackDownloadTimeout = "trackDownloadTimeout";
    public const string NisStartTimeout = "nisStartTimeout";
    public const string EventResultsTimeout = "eventResultsTimeout";
    public const string InactivityTimeout = "inactivityTimout"; // Intentionally mis-spelled
    public const string EventSettingsTimeout = "EventSettingsTimeout";
}