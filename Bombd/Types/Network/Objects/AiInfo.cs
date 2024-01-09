using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class AiInfo : INetworkWritable
{
    public const int MaxDataSize = 10;
    
    public readonly Platform Platform;
    public readonly int Count;
    
    public readonly NetAiData[] DataSet = new NetAiData[MaxDataSize];
    
    public AiInfo(Platform platform)
    {
        Platform = platform;
        Count = 0;
        for (int i = 0; i < DataSet.Length; ++i)
            DataSet[i] = new NetAiData();
    }
    
    public AiInfo(Platform platform, IReadOnlyList<string> players, int count = MaxDataSize)
    {
        Platform = platform;
        Count = count;
        
        List<AiDefinition> definitions = AiDefinition.GetRandomDefinitions(platform, count);
        
        for (int i = 0, player = 0; i < DataSet.Length; ++i, player++)
        {
            var ai = new NetAiData();
            if (count > i)
            {
                ai.OwnerName = players[player % players.Count];
                ai.UidName = "online_ai_" + i;
                ai.AiName = definitions[i].Name;
                ai.AiProfile = definitions[i].Profile;
            }

            DataSet[i] = ai;
        }
    }
    
    public void Write(NetworkWriter writer)
    {
        if (Platform == Platform.Karting)
        {
            foreach (NetAiData ai in DataSet)
            {
                writer.Write(ai.OwnerName);
                writer.Write(ai.UidName);
                writer.Write(ai.AiName);
                writer.Write(ai.AiProfile);
            }
        }
        else
        {
            foreach (NetAiData ai in DataSet)
            {
                writer.Write(ai.OwnerName, 32);
                writer.Write(ai.UidName, 128);
                writer.Write(ai.AiName, 32);
                writer.Write(ai.AiProfile, 32);
            }   
        }
    }
    
    public class NetAiData
    {
        public string AiName = string.Empty;
        public string AiProfile = string.Empty;
        public string OwnerName = string.Empty;
        public string UidName = string.Empty;
    }
}