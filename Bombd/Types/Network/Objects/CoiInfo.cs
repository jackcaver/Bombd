using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class CoiInfo : INetworkWritable
{
    public CoiSingleEvent Hotseat = new("Hotseat", 2);
    public CoiSingleEvent DLC = new("DLC Demo", 3);
    public CoiSeriesEvent Showcase = new("Showcase", 0);
    public CoiSeriesEvent Themed = new("Special Event", 1);
    
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
    public EventSettings Event = new();
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(Name, 0x40);
        writer.Write(Url, 0x80);
        writer.Write(Index);
        writer.Write(Event);
    }
}