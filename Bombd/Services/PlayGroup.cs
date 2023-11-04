using Bombd.Attributes;
using Bombd.Core;
using Bombd.Protocols;

namespace Bombd.Services;

[Service("playgroup", 10514, ProtocolType.TCP)]
public class PlayGroup : BombdService
{
}