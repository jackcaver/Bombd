namespace Bombd.Types.Network;


// karting
// Difficulty
// 1 = Casual
// 2 = Normal

// Type
// 0 = Any
// 1 = Action
// 2 = Pure
// 3 = Battle
// 4 = Custom

// Series
// 0 = Any
// 1 = Single
// 2 = Series

// Speed
// 0 = Any
// 1 = Fast
// 2 = Faster
// 3 = Fastest

// Track Group
// 0 = Any
// 1 = Official

// mnr
// 0 = pure
// 1 = action
// 2 = last kar
// 3 = time trial
// 4 = hot seat
// 5 = thug race
// 6 = score attack


public enum RaceType : int
{
    Pure,
    Action,
    Battle,
    TimeTrial,
    HotSeat,
    ThugRace,
    ScoreAttack
}