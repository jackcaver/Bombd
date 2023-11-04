namespace Bombd.Types.Network.Objects;

public class AiDefinition
{
    public static AiDefinition[] ModnationDefinitions =
    {
        new() { Name = "Shadow", Profile = "SHADOWonVISTAPOINT" },
        new() { Name = "Fade", Profile = "FADEonMARKETRUN" },
        new() { Name = "Dolor", Profile = "DOLORonRUMBLEISLAND" },
        new() { Name = "Nato", Profile = "NATOonSINKHOLE" },
        new() { Name = "Drillbit", Profile = "DrillbitonMINERSRIFT" },
        new() { Name = "Iceman", Profile = "ICEMANonCRAGGYHILLS" },
        new() { Name = "Skidplate", Profile = "SKIDPLATEonBOARDWALK" },
        new() { Name = "Wildcard", Profile = "WILDCARDonMOTOCROSSISLAND" },
        new() { Name = "Hale", Profile = "HALEonFLAMINGJUMPS" },
        new() { Name = "Scout", Profile = "SCOUTonRICKETYBRIDGE" },
        new() { Name = "Slick", Profile = "SLICKonSANDSTORM" },
        new() { Name = "Aloha", Profile = "ALOHAonTEMPLEOFTIKI" },
        new() { Name = "Dyno", Profile = "DYNOonMARINA" },
        new() { Name = "Espresso", Profile = "MAX" },
        new() { Name = "Diablo", Profile = "DIABLOonSPEEDWAYSPRINGS" },
        new() { Name = "A-Mach", Profile = "MID" },
        new() { Name = "Lolley", Profile = "MID" },
        new() { Name = "Cosmo", Profile = "MID" },
        new() { Name = "Parka", Profile = "MID" },
        new() { Name = "Emao III", Profile = "MID" },
        new() { Name = "JZee", Profile = "MID" },
        new() { Name = "Shotz", Profile = "MID" },
        new() { Name = "S to the Eth", Profile = "MID" },
        new() { Name = "Gunder", Profile = "MID" },
        new() { Name = "Tobias Roy", Profile = "MID" },
        new() { Name = "Radu", Profile = "MID" },
        new() { Name = "Lubker", Profile = "MID" },
        new() { Name = "Spence-O-Matic", Profile = "MID" },
        new() { Name = "The Soch", Profile = "MID" },
        new() { Name = "Kozak", Profile = "MID" },
        new() { Name = "E Man", Profile = "MID" },
        new() { Name = "Vlad", Profile = "MID" },
        new() { Name = "Prime Time", Profile = "MID" },
        new() { Name = "Joesiv", Profile = "MID" },
        new() { Name = "Oz", Profile = "MID" },
        new() { Name = "Gedski", Profile = "MID" },
        new() { Name = "Chainsaw", Profile = "MID" },
        new() { Name = "Bruzer", Profile = "MID" },
        new() { Name = "Granny", Profile = "MID" },
        new() { Name = "T-Rask", Profile = "MID" },
        new() { Name = "Griller", Profile = "MID" },
        new() { Name = "The MC", Profile = "MID" },
        new() { Name = "Pip Cork", Profile = "MID" },
        new() { Name = "Hooleyman", Profile = "MID" },
        new() { Name = "Kibbler", Profile = "MID" },
        new() { Name = "Skye", Profile = "MID" },
        new() { Name = "Lish", Profile = "MID" },
        new() { Name = "Mic", Profile = "MID" },
        new() { Name = "Harry", Profile = "MID" },
        new() { Name = "Astro Kitty", Profile = "MID" },
        new() { Name = "PJ", Profile = "MID" },
        new() { Name = "Captain Venus", Profile = "MID" },
        new() { Name = "Gill", Profile = "MID" },
        new() { Name = "Shaman", Profile = "MID" },
        new() { Name = "Bionic", Profile = "MID" },
        new() { Name = "A.N.T.", Profile = "MID" },
        new() { Name = "Gargles", Profile = "MID" },
        new() { Name = "Nails", Profile = "MID" },
        new() { Name = "Abranda", Profile = "MID" },
        new() { Name = "Marvin", Profile = "THUG" },
        new() { Name = "Robbie", Profile = "THUG" },
        new() { Name = "Max", Profile = "THUG" },
        new() { Name = "Hal", Profile = "THUG" },
        new() { Name = "Sven", Profile = "THUG" },
        new() { Name = "Bjorn", Profile = "THUG" },
        new() { Name = "Gustav", Profile = "THUG" },
        new() { Name = "Erik", Profile = "THUG" },
        new() { Name = "Kristoffer", Profile = "THUG" },
        new() { Name = "Clyde", Profile = "THUG" },
        new() { Name = "Robin", Profile = "THUG" },
        new() { Name = "Jesse", Profile = "THUG" },
        new() { Name = "Carmen", Profile = "THUG" },
        new() { Name = "Butch", Profile = "THUG" },
        new() { Name = "Flynn", Profile = "THUG" },
        new() { Name = "Ricky", Profile = "THUG" },
        new() { Name = "Danny", Profile = "THUG" },
        new() { Name = "Billy", Profile = "THUG" },
        new() { Name = "Bobby", Profile = "THUG" },
        new() { Name = "Johnny", Profile = "THUG" },
        new() { Name = "Andy", Profile = "THUG" },
        new() { Name = "Timmy", Profile = "THUG" },
        new() { Name = "Beezel", Profile = "THUG" },
        new() { Name = "Deezel", Profile = "THUG" },
        new() { Name = "Weezel", Profile = "THUG" },
        new() { Name = "Meezel", Profile = "THUG" },
        new() { Name = "Teezel", Profile = "THUG" },
        new() { Name = "Zeezel", Profile = "THUG" },
        new() { Name = "Neezel", Profile = "THUG" },
        new() { Name = "Reezel", Profile = "THUG" }
    };

    public string Name;
    public string Profile;

    public static List<AiDefinition> GetRandomDefinitions(int count)
    {
        List<AiDefinition> definitions = new();
        HashSet<int> selected = new();

        var random = new Random();
        while (definitions.Count != count)
        {
            int index = random.Next(0, ModnationDefinitions.Length);
            if (selected.Contains(index)) continue;
            definitions.Add(ModnationDefinitions[index]);
            selected.Add(index);
        }

        return definitions;
    }
}