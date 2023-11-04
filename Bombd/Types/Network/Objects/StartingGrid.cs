using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class StartingGrid : List<int>, INetworkWritable
{
    public void Write(NetworkWriter writer)
    {
        foreach (int nameUid in this)
        {
            writer.Write(nameUid);
            writer.Write(0);
        }

        writer.Clear(8 * (12 - Count));
    }
}