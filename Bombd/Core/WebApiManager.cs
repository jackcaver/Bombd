using System.Text;
using System.Text.Json;
using System.Xml;
using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Types.Network.Objects;
using Bombd.Types.Network.Races;
using Bombd.Types.ServerCommunication;

namespace Bombd.Core;

public class WebApiManager
{
    private static string? ContentUpdatesURL;
    
    private static string MakeRequest(string url)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        try
        {
            var response = client.Send(request);
            if (!response.IsSuccessStatusCode) return string.Empty;
            
            using var reader = new StreamReader(response.Content.ReadAsStream());
            return reader.ReadToEnd();
        }
        catch (Exception)
        {
            Logger.LogError<WebApiManager>($"Request to {url} failed!");
            return string.Empty;
        }
    }
    
    public static EventSettings GetSingleEvent(string type)
    {
        List<EventSettings> settings = GetSeriesEvent(type);
        // Default EventSettings constructor is track studio.
        return settings.Count == 0 ? new EventSettings(Platform.ModNation) : settings[0];
    }
    
    public static List<EventSettings> GetSeriesEvent(string type)
    {
        var events = new List<EventSettings>();
        if (string.IsNullOrEmpty(ContentUpdatesURL)) return events;
        
        string xml =
            MakeRequest(
                $"{BombdConfig.Instance.ApiURL}/{ContentUpdatesURL}?platform=PS3&content_update_type={type}");
        if (string.IsNullOrEmpty(xml)) return events;

        try
        {
            var contentDocument = new XmlDocument();
            var eventDocument = new XmlDocument();
            contentDocument.LoadXml(xml);
            var tags = contentDocument.GetElementsByTagName("data");
            if (tags.Count == 0) return events;
            
            string data = Encoding.UTF8.GetString(Convert.FromBase64String(tags[0].InnerText));
            eventDocument.LoadXml(data);
            
            foreach (XmlElement evt in eventDocument.GetElementsByTagName("event"))
            {
                var attributes = evt.Attributes;
                events.Add(new EventSettings(Platform.ModNation)
                {
                    TrackName = attributes["name"].Value,
                    CreationId = int.Parse(attributes["id"].Value),
                    NumLaps = int.Parse(attributes["laps"].Value),
                });   
            }
        }
        catch (Exception)
        {
            Logger.LogError<WebApiManager>("An error occurred parsing content updates!");
        }
        
        return events;
    }

    public static List<EventSettings> GetTopTracks()
    {
        var events = GetSeriesEvent("THEMED_EVENTS");
        for (int i = 0; i < events.Count; ++i)
        {
            var settings = events[i];
            settings.CareerEventIndex = CoiInfo.SPHERE_INDEX_TOP_TRACKS;
            settings.SeriesEventIndex = i;
            settings.AiEnabled = false;
        }

        return events;
    }

    public static EventSettings GetHotSeat()
    {
        var settings = GetSingleEvent("HOT_SEAT_PLAYLIST");
        settings.CareerEventIndex = CoiInfo.SPHERE_INDEX_HOTSEAT;
        settings.RaceType = RaceType.HotSeat;
        return settings;
    }

    public static CoiInfo GetCircleOfInfluence()
    {
        var coi = new CoiInfo
        {
            Hotseat =
            {
                Event = GetHotSeat()
            },
            Themed =
            {
                Events = GetTopTracks()
            }
        };

        return coi;
    }
    
    public static SeriesInfo GetTopTrackSeries(int owner, string kartParkHome)
    {
        var info = new SeriesInfo(Platform.ModNation);
        var events = GetTopTracks();
        foreach (var settings in events)
        {
            settings.OwnerNetcodeUserId = owner;
            settings.KartParkHome = kartParkHome;
            info.Events.Add(settings);
        }
        
        return info;
    }

    public static void Initialize()
    {
        if (string.IsNullOrEmpty(BombdConfig.Instance.ApiURL))
        {
            Logger.LogWarning<WebApiManager>("No Web API URL provided!");
            return;
        }
        
        // Strip the trailing slash from the URL if someone left it in
        string url = BombdConfig.Instance.ApiURL.TrimEnd('/');
        BombdConfig.Instance.ApiURL = url;
        
        string xml = MakeRequest($"{url}/resources/content_update.latest.xml");
        if (string.IsNullOrEmpty(xml))
        {
            Logger.LogWarning<WebApiManager>("Content updates XML was empty!");
            return;
        }
        
        try
        {
            var document = new XmlDocument();
            document.LoadXml(xml);

            var tags = document.GetElementsByTagName("request");
            if (tags.Count == 0)
            {
                Logger.LogWarning<WebApiManager>("No content updates URL in XML!");
                return;
            }
            
            ContentUpdatesURL = tags[0].Attributes["url"].Value;
        }
        catch (Exception)
        {
            Logger.LogError<WebApiManager>("Failed to parse content updates XML!");
        }
    }

    public static bool CheckSessionStatus(Guid SessionID)
    {
        var message = new Message
        {
            Type = "CheckPlayerSession",
            From = BombdServer.Instance.ClusterUuid,
            To = "API",
            Content = SessionID.ToString()
        };
        ServerCommunication.Send(JsonSerializer.Serialize(message)).Wait();
        
        bool Responded = false;
        PlayerSessionStatus? status = new PlayerSessionStatus();
        while (!Responded)
        {
            var Responses = ServerCommunication.MessageBuffer.Where(match => match.From == "API"
                && (match.Type == "PlayerSessionStatus" || match.Type == "CheckPlayerSessionError"));
            foreach (var response in Responses)
            {
                if (response.Type == "PlayerSessionStatus")
                {
                    status = JsonSerializer.Deserialize<PlayerSessionStatus>(response.Content);
                    if (status != null && status.SessionID == SessionID)
                    {
                        Responded = true;
                        break;
                    }
                }
                else
                {
                    Logger.LogError<ServerCommunication>($"There was an error checking session status: {message.Content}");
                }
            }
        }

        return status != null && status.IsAuthorized;
    }
}