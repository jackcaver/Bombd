using Bombd.Serialization;

namespace Bombd.Types.GameBrowser;

public class GameSearchAttributes : GameAttributes
{
    public override void Read(NetworkReader reader)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; ++i)
        {
            string key = reader.ReadString();
            string value = reader.ReadString();
            if (!string.IsNullOrEmpty(key)) Add(key, value);
        }
    }
}