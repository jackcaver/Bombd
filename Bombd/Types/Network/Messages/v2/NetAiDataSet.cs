using System.Runtime.CompilerServices;
using Bombd.Types.Network.Objects;

namespace Bombd.Types.Network.Messages.v2;

[InlineArray(10)]
public struct NetAiDataSet
{
    private AiInfo.NetAiData _element;
}