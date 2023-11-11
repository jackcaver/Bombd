using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class StartingGrid : List<int>, INetworkWritable
{
    public readonly Platform Platform;
    public StartingGrid(Platform platform) => Platform = platform;
    
    public void Write(NetworkWriter writer)
    {
        if (Platform == Platform.ModNation)
        {
            foreach (int nameUid in this)
            {
                writer.Write(nameUid);
                writer.Write(0);
            }
            writer.Clear(8 * (12 - Count));
            return;
        }

        foreach (int nameUid in this)
        {
            writer.Write(nameUid);
            writer.Write(false);
        }
        
        writer.Clear(5 * (12 - Count));
    }
}