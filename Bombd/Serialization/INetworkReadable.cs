namespace Bombd.Serialization;

public interface INetworkReadable
{
    void Read(NetworkReader reader);
}