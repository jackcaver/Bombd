using Bombd.Serialization;

namespace Bombd.Types.Network.NetObjects;

public class StartingGrid : List<int>, INetworkWritable
{
    public void Write(NetworkWriter writer)
    {
        foreach (var nameUid in this)
        {
            writer.Write(nameUid);
            writer.Write(0);
        }
        
        writer.Clear(8 * (12 - Count));
        
    }
}