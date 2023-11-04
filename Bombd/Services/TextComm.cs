using Bombd.Attributes;
using Bombd.Core;
using Bombd.Protocols;

namespace Bombd.Services;

[Service("textcomm", 10513, ProtocolType.TCP)]
public class TextComm : BombdService
{
}