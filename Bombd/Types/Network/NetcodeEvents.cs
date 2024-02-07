namespace Bombd.Types.Network;

public enum NetcodeEvents
{
    OnGameJoined,
    OnGameJoinFailure,
    OnGameLeft,
    OnGameDestroyed,
    OnGameCreated,
    OnGameReserved,
    OnGameReserveFail,
    OnGameLeaveFailure,
    OnGameCreateFailure,
    OnGameAttributeUpdate,
    OnReserveSlotsInGameSuccess,
    OnReserveSlotsInGameFailure,
    OnPlayerJoinedGame,
    OnPlayerLeftGame,
    OnKickedFromGame,
    OnGameShutdown,
    OnMatchmakingBegin,
    OnMatchmakingCanceled,
    OnMatchmakingError,
    OnMatchmakingJoinGameBegin
}