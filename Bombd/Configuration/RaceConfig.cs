using Bombd.Core;

namespace Bombd.Configuration;

public class RaceConfig
{
    public static readonly RaceConfig Instance = BombdConfig.Instance.RaceSettings;
    
    /// <summary>
    ///     How long the 3...2...1... timer lasts
    /// </summary>
    public int EventCountdownTime { get; set; } = 5000;
    
    public RaceConstants ModNation { get; set; } = new()
    {
        GameRoomCountdownTime = 30000,
        GameRoomTimerRacerLock = 25000,
        GameRoomTimerLock = 20000,
        PostRaceTime = 15000
    };
    
    public RaceConstants Karting { get; set; } = new()
    {
        GameRoomCountdownTime = 60000,
        GameRoomTimerRacerLock = 5000,
        GameRoomTimerLock = 5000,
        PostRaceTime = 45000
    };
}