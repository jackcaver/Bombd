namespace Bombd.Configuration;

public class RaceConstants
{
    /// <summary>
    ///     How long the countdown timer in rooms last in milliseconds
    /// </summary>
    public int GameRoomCountdownTime { get; set; }
        
    /// <summary>
    ///     The countdown time where players can no longer join the game in milliseconds
    /// </summary>
    public float GameRoomTimerRacerLock { get; set; } = 25000.0f;
        
    /// <summary>
    ///     The countdown time left before players can no longer join in milliseconds
    /// </summary>
    public float GameRoomTimerLock { get; set; }
        
        
    /// <summary>
    ///     How long the post-race scoreboard and voting lasts in milliseconds
    /// </summary>
    public int PostRaceTime { get; set; }
}