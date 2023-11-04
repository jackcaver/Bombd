namespace Bombd.Types.Authentication;

public enum TicketData : ushort
{
    Empty = 0,
    U32 = 1,
    U64 = 2,
    String = 4,
    Time = 7,
    Binary = 8
}