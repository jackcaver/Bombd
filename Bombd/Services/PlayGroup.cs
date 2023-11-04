using Bombd.Attributes;
using Bombd.Protocols;
using Bombd.Types.Services;

namespace Bombd.Services;

[Service("playgroup", 10514, ProtocolType.TCP)]
public class PlayGroup : BombdService
{
}