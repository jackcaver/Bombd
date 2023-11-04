namespace Bombd.Types.Requests;

public struct PlayerJoinRequest
{
    public int Timestamp;
    public int UserId;
    public string GameName;
    public int ReservationKey;
    public string? Guest;
}