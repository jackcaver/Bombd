using Bombd.Attributes;
using Bombd.Core;
using Bombd.Protocols;

namespace Bombd.Services;

[Service("stats", 13452, ProtocolType.TCP)]
public class Stats : BombdService
{
}