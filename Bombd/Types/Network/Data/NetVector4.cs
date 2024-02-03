using System.Numerics;
using Bombd.Serialization;

namespace Bombd.Types.Network.Data;

public struct NetVector4 : INetworkReadable, INetworkWritable
{
    private int _packed;

    public NetVector4()
    {
    }
    
    public NetVector4(int packed) => _packed = packed;

    public NetVector4(float min, float max, Vector4 vector)
    {
        _packed = 0;
        _packed |= PackMember(min, max, vector.X) << 24;
        _packed |= PackMember(min, max, vector.Y) << 16;
        _packed |= PackMember(min, max, vector.Z) << 8;
        _packed |= PackMember(min, max, vector.W) << 0;
    }

    public Vector4 Unpack(float min, float max)
    {
        return new Vector4(
            UnpackMember(min, max, (byte)(_packed >> 24)),
            UnpackMember(min, max, (byte)(_packed >> 16)),
            UnpackMember(min, max, (byte)(_packed >> 8)),
            UnpackMember(min, max, (byte)(_packed >> 0)));
    }
    
    private static byte PackMember(float min, float max, float member)
    {
        float v = (member - min) / (max - min);
        if (1.0 - v < 0.0) v = 1.0f;
        if (v - 0.0f < 0.0f) v = 0.0f;
        return (byte)(int)(v * 255.0f);
    }
    
    private static float UnpackMember(float min, float max, byte member)
    {
        return member * (1.0f / 255.0f) * (max - min) + min;
    }
    
    public void Read(NetworkReader reader) => _packed = reader.ReadInt32();
    public void Write(NetworkWriter writer) => writer.Write(_packed);
}