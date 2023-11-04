using Bombd.Serialization;

namespace Bombd.Types.Network.Messages;

public struct NetMessageRandomSeed : INetworkMessage
{
    public NetMessageType Type => NetMessageType.RandomSeed;

    public int Seed;

    public void Write(NetworkWriter writer)
    {
        writer.Write(Seed);
    }
}