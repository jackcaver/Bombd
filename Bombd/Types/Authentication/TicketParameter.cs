namespace Bombd.Types.Authentication;

public struct TicketParameter
{
    public TicketData DataType;
    public ArraySegment<byte> Data;
}