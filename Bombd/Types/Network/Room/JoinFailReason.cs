namespace Bombd.Types.Network.Room;

public class JoinFailReason
{
    public const string NoGamesAvailable = "noGamesAvailable";
    public const string Timeout = "timeout";
    public const string FailedServiceSwitch = "serviceSwitchFailSessionKey";
    public const string GameFull = "gameFull";
    public const string NoPlayersMigrated = "noPlayersMigrated";
    public const string GameNotFound = "gameNotFound";
    public const string GameClosed = "gameClosedToJoin";
    public const string NotEnoughSlots = "notEnoughReservableSlots";
}