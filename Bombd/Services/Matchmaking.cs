using Bombd.Attributes;
using Bombd.Core;
using Bombd.Protocols;

namespace Bombd.Services;

[Service("matchmaking", 10510, ProtocolType.TCP)]
public class Matchmaking : BombdService
{
}