using Bombd.Serialization;
using Bombd.Simulation;

namespace Bombd.Types.Network;

public class GuestStatusBlock : List<GuestStatus>, INetworkReadable, INetworkWritable
{
    public void Read(NetworkReader reader)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; ++i)
            Add(reader.Read<GuestStatus>());
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write(Count);
        foreach (GuestStatus guest in this)
            writer.Write(guest);
    }
}