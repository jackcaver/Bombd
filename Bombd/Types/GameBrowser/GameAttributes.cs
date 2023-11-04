using Bombd.Serialization;

namespace Bombd.Types.GameBrowser;

public class GameAttributes : Dictionary<string, string>, INetworkReadable, INetworkWritable
{
    private const int MaxAttributes = 32;
    private const int MaxAttributeSize = 32;
    private const int AttributePairBinarySize = 0x44;

    public GameAttributes() : base(MaxAttributes)
    {
    }

    public void Read(NetworkReader reader)
    {
        // When this structure is being read/written from memory,
        // it's directly memcpy'd into the structure, so there's padding
        // between some of the members that needs to be accounted for.
        reader.ReadInt32();
        for (int i = 0; i < MaxAttributes; ++i)
        {
            reader.ReadInt32();
            string key = reader.ReadString(MaxAttributeSize);
            string value = reader.ReadString(MaxAttributeSize);
            if (!string.IsNullOrEmpty(key)) Add(key, value);
        }
    }

    public void Write(NetworkWriter writer)
    {
        // When writing this structure back to the client, it reads it back
        // in a standard way, rather than sending the raw structure data.
        int len = Math.Min(Count, MaxAttributes);
        writer.Write(len);
        for (int i = 0; i < len; ++i)
        {
            KeyValuePair<string, string> attribute = this.ElementAt(i);
            writer.Write(attribute.Key);
            writer.Write(attribute.Value);
        }
    }
}