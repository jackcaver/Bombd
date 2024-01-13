using Bombd.Serialization;

namespace Bombd.Types.Network.Room;

public class GuestStatus : INetworkReadable, INetworkWritable
{
    public string Username = string.Empty;
    public GuestStatusCode Status = GuestStatusCode.AttachSuccess;
    public string StatusMessage = string.Empty;
    
    public void Read(NetworkReader reader)
    {
        Username = reader.ReadString();
        Status = (GuestStatusCode)reader.ReadInt32();
        StatusMessage = reader.ReadString();
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write(Username);
        writer.Write((int)Status);
        writer.Write(StatusMessage);
    }
}