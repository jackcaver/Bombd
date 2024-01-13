namespace Bombd.Types.Network.Races;

public enum EventUpdateReason
{
    AdvanceTimer,
    PauseTimer, 
    HostChanged, // 2
    HostVetoed, // 3,
    RaceSettingsChanged, // 4
    RaceSettingsVetoed,  // 5
    PlayerKicked,
    TeamChange,
    TeamLeave
}