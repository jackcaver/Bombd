using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class StartingGrid : List<GridPositionData>, INetworkWritable
{
    public readonly Platform Platform;
    public StartingGrid(Platform platform) => Platform = platform;
    
    public void Write(NetworkWriter writer)
    {
        int len = Math.Min(Count, 12);
        if (Platform == Platform.ModNation)
        {
            for (int i = 0; i < len; ++i)
            {
                GridPositionData position = this.ElementAt(i);
                writer.Write(position.NameUid);
                writer.Write(position.IsGuestPosition ? 1 : 0);
            }
            writer.Clear(8 * (12 - len));
            return;
        }
        
        for (int i = 0; i < len; ++i)
        {
            GridPositionData position = this.ElementAt(i);
            writer.Write(position.NameUid);
            writer.Write(position.IsGuestPosition);
        }
        writer.Clear(5 * (12 - Count));
    }
}

public struct GridPositionData
{
    public GridPositionData(uint uid, bool isGuest)
    {
        NameUid = uid;
        IsGuestPosition = isGuest;
    }
    
    public uint NameUid;
    public bool IsGuestPosition;
}