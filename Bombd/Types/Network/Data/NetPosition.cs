using System.Numerics;
using Bombd.Serialization;

namespace Bombd.Types.Network.Data;

public class NetPosition : INetworkReadable, INetworkWritable
{
    private const float Min = -512.0f;
    private const float Max = 512.0f;
    
    private ushort _x;
    private ushort _y;
    private ushort _z;

    public NetPosition()
    {
    }

    public NetPosition(ushort x, ushort y, ushort z)
    {
        _x = x;
        _y = y;
        _z = z;
    }

    public NetPosition(Vector3 vector)
    {
        _x = PackMember(vector.X);
        _y = PackMember(vector.Y);
        _z = PackMember(vector.Z);
    }
    
    public Vector3 Unpack()
    {
        return new Vector3(UnpackMember(_x), UnpackMember(_y), UnpackMember(_z));
    }

    private static ushort PackMember(float member)
    {
        float v = (member - Min) / (Max - Min);
        if (1.0 - v < 0.0) v = 1.0f;
        if (v - 0.0f < 0.0f) v = 0.0f;
        return (ushort)(int)(v * 65535.0f);
    }

    private static float UnpackMember(ushort member)
    {
        return member * (1.0f / 65535.0f) * (Max - Min) + Min;
    }

    public void Read(NetworkReader reader)
    {
        _x = reader.ReadUInt16();
        _y = reader.ReadUInt16();
        _z = reader.ReadUInt16();
    }

    public void Write(NetworkWriter writer)
    {
        writer.Write(_x);
        writer.Write(_y);
        writer.Write(_z);
    }
}