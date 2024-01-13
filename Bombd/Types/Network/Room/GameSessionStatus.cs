namespace Bombd.Types.Network.Room;

public enum GameSessionStatus
{
    JoinAsSpectator = 0,
    JoinAsRacer = 1,
    SwitchSpectatorToRacer = 2,
    SwitchRacerToSpectator = 3,
    SwitchAllToRacer = 4
}