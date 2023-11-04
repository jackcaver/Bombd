using Bombd.Attributes;
using Bombd.Protocols;
using Bombd.Types.Services;

namespace Bombd.Services;

[Service("textcomm", 10513, ProtocolType.TCP)]
public class TextComm : BombdService
{
}