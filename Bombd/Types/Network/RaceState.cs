namespace Bombd.Types.Network;

public enum RaceState : int
{
    Invalid,
    GameroomCountdown,
    LoadingIntoRace,
    Racing,
    WaitingForRaceEnd,
    PostRace,
    Nis
}