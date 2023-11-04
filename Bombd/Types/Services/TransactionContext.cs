using Bombd.Protocols;

namespace Bombd.Types.Services;

public class TransactionContext
{
    public ConnectionBase Connection { get; init; }
    public Session Session { get; init; }
    public NetcodeTransaction Request { get; init; }
    public NetcodeTransaction Response { get; init; }
}