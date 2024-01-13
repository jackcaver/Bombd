namespace Bombd.Types.Network.Races;

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