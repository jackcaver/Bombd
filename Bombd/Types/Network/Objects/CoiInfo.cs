using Bombd.Helpers;
using Bombd.Serialization;
using Bombd.Types.Network.Races;

namespace Bombd.Types.Network.Objects;

public class CoiInfo : INetworkWritable
{
    public const int SPHERE_INDEX_SHOWCASE = 0;
    public const int SPHERE_INDEX_TOP_TRACKS = 1;
    public const int SPHERE_INDEX_HOTSEAT = 2;
    public const int SPHERE_INDEX_DLC_DEMO = 3;
    
    public CoiSingleEvent Hotseat = new("Hotseat", SPHERE_INDEX_HOTSEAT);
    public CoiSingleEvent DLC = new("DLC Demo", SPHERE_INDEX_DLC_DEMO);
    public CoiSeriesEvent Showcase = new("Showcase", SPHERE_INDEX_SHOWCASE);
    public CoiSeriesEvent Themed = new("Special Event", SPHERE_INDEX_TOP_TRACKS);

    public void Write(NetworkWriter writer)
    {
        writer.Write(Hotseat);
        writer.Write(DLC);
        writer.Write(Showcase);
        writer.Write(Themed);
    }
}

public class CoiSeriesEvent : INetworkWritable
{
    public CoiSeriesEvent(string name, int index)
    {
        Name = name;
        Index = index;
    }
    
    public string Name;
    public string Url = "http://www.modnation.com";
    public int Index;
    public List<EventSettings> Events = new();
        
    public void Write(NetworkWriter writer)
    {
        writer.Write(Name, 0x40);
        writer.Write(Url, 0x80);
        writer.Write(Index);
        // Series support up to 10 events.
        int len = Math.Min(10, Events.Count);
        for (int i = 0; i < len; ++i) writer.Write(Events[i]);
        if (len != 10)
        {
            writer.Clear(0x14c * (10 - len));
        }
    }
}

public class CoiSingleEvent : INetworkWritable
{
    public CoiSingleEvent(string name, int index)
    {
        Name = name;
        Index = index;
    }
    
    public string Name;
    public string Url = "http://www.modnation.com";
    public int Index;
    public EventSettings Event = new(Platform.ModNation);
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(Name, 0x40);
        writer.Write(Url, 0x80);
        writer.Write(Index);
        writer.Write(Event);
    }
}