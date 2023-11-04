using Bombd.Attributes;
using Bombd.Protocols;
using Bombd.Types.Services;

namespace Bombd.Services;

[Service("matchmaking", 10510, ProtocolType.TCP)]
public class Matchmaking : BombdService
{
}