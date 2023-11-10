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
    ///     How long the countdown timer in gamerooms last
    /// </summary>
    public int GameroomCountdownTime { get; set; } = 5000;

    /// <summary>
    ///     The countdown time where players can no longer join the gameroom.
    /// </summary>
    public float GameroomRacerLockTime { get; set; } = 5000.0f;

    /// <summary>
    ///     The countdown time where the timer can no longer be paused.
    /// </summary>
    public float GameroomTimerLockTime { get; set; } = 5000.0f;

    /// <summary>
    ///     How many times each service performs an update in a second.
    /// </summary>
    public int TickRate { get; set; } = 15;
}