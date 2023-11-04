using Bombd.Serialization;

namespace Bombd.Types.Network;

public struct GenericInt32 : INetworkWritable, INetworkReadable
{
    private int _data;

    public GenericInt32()
    {
    }

    public GenericInt32(int value)
    {
        _data = value;
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write(_data);
    }

    public void Read(NetworkReader reader)
    {
        _data = reader.ReadInt32();
    }
    
    public static implicit operator int(GenericInt32 value) => value._data;

}