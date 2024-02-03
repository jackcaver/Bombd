using System.Xml.Serialization;
using Bombd.Helpers;
using Bombd.Types.Network.Objects;

namespace Bombd.Types.Story;

[XmlRoot("CareerData")]
public class CareerData
{
    private static readonly XmlSerializer CareerSerializer = new(typeof(CareerData));

    public Platform Platform;
    public List<CareerTrack> Tracks = new();
    public List<AiDefinition> AiDefinitions = new();
    [XmlIgnore] public List<CareerTrack> VotableTracks = new();

    public static CareerData Load(string file)
    {
        string data = File.ReadAllText(file);
        using var reader = new StringReader(data);
        var career = (CareerData) CareerSerializer.Deserialize(reader)!;
        career.VotableTracks = career.Tracks.Where(track => track.Votable).ToList();
        return career;
    }

    public EventSettings GetRankedEvent(int owner, int previousTrackId = -1)
    {
        CareerTrack track;
        var random = new Random();
        do track = Tracks[random.Next(0, Tracks.Count)];
        while (track.Id == previousTrackId);
        
        return new EventSettings(Platform, track)
        {
            AiEnabled = false,
            OwnerNetcodeUserId = owner,
            IsRanked = true
        };
    }
    
    public SeriesInfo GetRankedSeries(int count, int owner)
    {
        var info = new SeriesInfo(Platform);
        HashSet<int> selected = new();
        var random = new Random();
        int seriesIndex = 0;
        while (info.Events.Count != count)
        {
            int index = random.Next(0, Tracks.Count);
            if (selected.Contains(index)) continue;
            selected.Add(index);
            
            info.Events.Add(new EventSettings(Platform, Tracks[index])
            {
                SeriesEventIndex = seriesIndex++,
                AiEnabled = false,
                OwnerNetcodeUserId = owner,
                IsRanked = true
            });
        }
        
        return info;
    }

    public List<int> GetVotePackage(int previousTrackId)
    {
        HashSet<int> selected =
        [
            previousTrackId
        ];
        List<int> tracks = new(3);
        
        var random = new Random();
        while (tracks.Count != 3)
        {
            int trackId = VotableTracks[random.Next(0, VotableTracks.Count)].Id;
            if (selected.Contains(trackId)) continue;
            tracks.Add(trackId);
            selected.Add(trackId);
        }

        return tracks;
    }

    public List<AiDefinition> GetRandomAi(int count)
    {
        List<AiDefinition> definitions = new();
        HashSet<int> selected = new();

        var random = new Random();
        while (definitions.Count != count)
        {
            int index = random.Next(0, AiDefinitions.Count);
            if (selected.Contains(index)) continue;
            definitions.Add(AiDefinitions[index]);
            selected.Add(index);
        }

        return definitions;
    }
}