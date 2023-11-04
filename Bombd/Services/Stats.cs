using Bombd.Attributes;
using Bombd.Protocols;
using Bombd.Types.Services;

namespace Bombd.Services;

[Service("stats", 13452, ProtocolType.TCP)]
public class Stats : BombdService
{
}