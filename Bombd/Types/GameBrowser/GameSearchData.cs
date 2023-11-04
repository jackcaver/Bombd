using Bombd.Serialization;

namespace Bombd.Types.GameBrowser;

public class GameSearchData : INetworkReadable
{
    public int MatchmakingConfigFileVersion;
    public GameAttributes Attributes = new();
    public List<string> PreferredPlayerList = new();
    public int FreeSlotsRequired;
    public List<int> PreferredCreationIdList = new();

    public void Read(NetworkReader reader)
    {
        MatchmakingConfigFileVersion = reader.ReadInt32();

        int attributeCount = reader.ReadInt32();
        for (int i = 0; i < attributeCount; ++i)
        {
            string key = reader.ReadString(0x20);
            string value = reader.ReadString(0x20);
            if (!string.IsNullOrEmpty(key)) Attributes[key] = value;
        }

        int playerCount = reader.ReadInt32();
        for (int i = 0; i < playerCount; ++i) PreferredPlayerList.Add(reader.ReadString());
        
        FreeSlotsRequired = reader.ReadInt32();
        
        int creationCount = reader.ReadInt32();
        for (int i = 0; i < creationCount; ++i)
            PreferredCreationIdList.Add(reader.ReadInt32());
    }
}