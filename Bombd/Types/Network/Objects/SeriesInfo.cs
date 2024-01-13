using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class SeriesInfo : INetworkWritable, INetworkReadable
{
    public const int MaxEvents = 10;
    public readonly Platform Platform;
    public readonly List<EventSettings> Events = new();
    private readonly EventSettings _invalidSeriesEvent;
    
    public SeriesInfo(Platform platform)
    {
        Platform = platform;
        _invalidSeriesEvent = new EventSettings(Platform)
        {
            CreationId = 0,
            TrackName = string.Empty,
            TranslatedTrackName = string.Empty
        };
    }
    
    public void Read(NetworkReader reader)
    {
        Events.Clear();
        for (int i = 0; i < MaxEvents; ++i)
        {
            var settings = new EventSettings(Platform);
            settings.Read(reader);
            Events.Add(settings);
        }
    }
    
    public void Write(NetworkWriter writer)
    {
        int len = Math.Min(MaxEvents, Events.Count);
        for (int i = 0; i < len; ++i)
        {
            Events[i].SeriesEventIndex = i;
            writer.Write(Events[i]);
        }
        
        if (len == MaxEvents) return;
        for (int i = 0; i < MaxEvents - len; ++i)
            writer.Write(_invalidSeriesEvent);
    }
}