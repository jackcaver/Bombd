namespace Bombd.Helpers;

public enum Platform
{
    Unknown = -1,
    Karting = 87659,
    ModNation = 186793
}

public static class GameTitleHelper
{
    private static readonly Dictionary<string, Platform> GameIdLookup = new();

    // NetcodeLibVersion = 3.4.20
    public static readonly string[] KartingTitleIds =
    {
        "NPUA70208",
        "BCES01423",
        "NPUA70249",
        "BCUS90565",
        "BCES01422",
        "NPHA80239",
        "BCAS20205",
        "NPUA80848",
        "BCET70048",
        "NPEA00421",
        "BCUS99141",
        "BCKS10235",
        "BCUS99089",
        "BCUS98254",
        "BCJS30085",
        "NPEA90117"
    };

    // NetcodeLibVersion = 3.3.1
    // NetcodeLibBuildRevision = 1733
    public static readonly string[] ModNationTitleIds =
    {
        "BCJS30041",
        "BCET70020",
        "NPUA70073",
        "BCUS98167",
        "BCKS10122",
        "BCES00764",
        "BCAS20105",
        "NPUA80535",
        "NPUA70096",
        "NPEA00291",
        "BCES00701",
        "NPEA90062",
        "NPUA70074"
    };

    static GameTitleHelper()
    {
        KartingTitleIds.ToList().ForEach(id => GameIdLookup.Add(id, Platform.Karting));
        ModNationTitleIds.ToList().ForEach(id => GameIdLookup.Add(id, Platform.ModNation));
    }

    public static Platform FromTitleId(string id) => GameIdLookup.GetValueOrDefault(id, Platform.Unknown);
}