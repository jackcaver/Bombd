namespace Bombd.Types.Requests;

public struct PlayerJoinRequest
{
    public int Timestamp;
    public string Username;
    public int UserId;
    public string GameName;
    public string? ReservationKey;
    public List<string> Guests;
}