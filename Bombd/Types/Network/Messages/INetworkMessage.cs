using Bombd.Serialization;

namespace Bombd.Types.Network.Messages;

public interface INetworkMessage : INetworkWritable
{
    public NetMessageType Type { get; }
}