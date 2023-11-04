namespace Bombd.Serialization;

public interface INetworkWritable
{
    void Write(NetworkWriter writer);
}