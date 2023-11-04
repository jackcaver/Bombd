using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class AiInfo : INetworkWritable
{
    public const int MaxDataSize = 10;
    public readonly int Count;

    public readonly NetAiData[] DataSet = new NetAiData[MaxDataSize];

    public AiInfo(int startingAiCount = MaxDataSize)
    {
        List<AiDefinition> definitions = AiDefinition.GetRandomDefinitions(startingAiCount);
        Count = startingAiCount;
        for (int i = 0; i < DataSet.Length; ++i)
        {
            var ai = new NetAiData();
            if (startingAiCount > i)
            {
                // ai.OwnerName = NetworkMessages.SimServerName;
                ai.OwnerName = "Arihzi";
                ai.UidName = "online_ai_" + i;
                ai.AiName = definitions[i].Name;
                ai.AiProfile = definitions[i].Profile;
            }

            DataSet[i] = ai;
        }
    }

    public void Write(NetworkWriter writer)
    {
        foreach (NetAiData ai in DataSet)
        {
            writer.Write(ai.OwnerName, 32);
            writer.Write(ai.UidName, 128);
            writer.Write(ai.AiName, 32);
            writer.Write(ai.AiProfile, 32);
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