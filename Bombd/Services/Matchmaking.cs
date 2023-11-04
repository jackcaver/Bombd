using Bombd.Attributes;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Protocols;
using Bombd.Types.Services;

namespace Bombd.Services;

[Service("matchmaking", 10510, ProtocolType.TCP)]
public class Matchmaking : BombdService
{
    [Transaction("beginMatchmaking")]
    public void BeginMatchmaking(TransactionContext context)
    {
        context.Response.MethodName = "matchmakingBegin";
        context.Response["matchmakingBeginTime"] = TimeHelper.LocalTime.ToString();
    }

    [Transaction("cancelMatchmaking")]
    public void CancelMatchmaking(TransactionContext context)
    {
        context.Response.MethodName = "matchmakingCanceled";
    }
}