using Bombd.Serialization;

namespace Bombd.Types.GameBrowser;

public class GameBrowserBusiestList : HashSet<int>, INetworkWritable
{
    public const int MaxSize = 100;
    
    public void Write(NetworkWriter writer)
    {
        int len = Math.Min(Count, MaxSize);
        writer.Write(len);
        for (int i = 0; i < len; ++i)
            writer.Write(this.ElementAt(i));
    }
}