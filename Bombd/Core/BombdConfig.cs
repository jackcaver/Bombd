namespace Bombd.Core;

public class BombdConfig
{
    public static readonly BombdConfig Instance = new();

    /// <summary>
    ///     The IP this server will listen on.
    /// </summary>
    public string ListenIP { get; set; } = "0.0.0.0";

    /// <summary>
    ///     The external IP of this server
    /// </summary>
    public string ExternalIP { get; set; } = "127.0.0.1";

    /// <summary>
    ///     The address of the Web API
    /// </summary>
    public string ApiURL { get; set; } = "http://127.0.0.1:10050";
    
    /// <summary>
    ///     How long the 3...2...1... timer lasts
    /// </summary>
    public int EventCountdownTime { get; set; } = 5000;

    /// <summary>
    ///     How long the countdown timer in ModNation gamerooms last
    /// </summary>
    public int ModnationGameroomCountdownTime { get; set; } = 30000;

    /// <summary>
    ///     The countdown time where players can no longer join the gameroom in ModNation Racers.
    /// </summary>
    public float ModnationGameroomRacerLockTime { get; set; } = 25000.0f;

    /// <summary>
    ///     The countdown time where the timer can no longer be paused in ModNation Racers.
    /// </summary>
    public float ModnationGameroomTimerLockTime { get; set; } = 20000.0f;

    /// <summary>
    ///     How long the countdown timer in LittleBigPlanet Karting gamerooms last
    /// </summary>
    public int KartingGameroomCountdownTime { get; set; } = 60000;

    /// <summary>
    ///     The countdown time left before players can no longer join in LittleBigPlanet Karting
    /// </summary>
    public float KartingGameroomTimerLock { get; set; } = 5000;

    /// <summary>
    ///     How long to give the players to vote after a race is completed in Karting.
    /// </summary>
    public int KartingPostRaceTime { get; set; } = 45000;

    /// <summary>
    ///     How long to give players to vote after a race is completed in ModNation.
    /// </summary>
    public int ModNationPostRaceTime { get; set; } = 15000;
    
    /// <summary>
    ///     Whether or not to force the minimum racer requirement of 2 in online races.
    /// </summary>
    public bool EnforceMinimumRacerRequirement { get; set; } = true;

    /// <summary>
    ///     How many times each service performs an update in a second.
    /// </summary>
    public int TickRate { get; set; } = 15;
}