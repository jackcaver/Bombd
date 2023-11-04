namespace Bombd.Extensions;

public static class ByteManipulationExtensions
{
    public static int WriteBoolean(this byte[] b, int offset, bool value)
    {
        b[offset] = (byte)(value ? 1 : 0);
        return 1;
    }

    public static int WriteUint8(this byte[] b, int offset, byte value)
    {
        b[offset] = value;
        return 1;
    }

    public static int WriteUint16BE(this byte[] b, int offset, ushort value)
    {
        b[offset + 0] = (byte)(value >> 8);
        b[offset + 1] = (byte)(value >> 0);

        return 2;
    }
    
    public static int WriteInt32BE(this byte[] b, int offset, int value)
    {
        b[offset + 0] = (byte)(value >> 24);
        b[offset + 1] = (byte)(value >> 16);
        b[offset + 2] = (byte)(value >> 8);
        b[offset + 3] = (byte)(value >> 0);

        return 4;
    }
    
    public static int WriteUint32BE(this byte[] b, int offset, uint value)
    {
        b[offset + 0] = (byte)(value >> 24);
        b[offset + 1] = (byte)(value >> 16);
        b[offset + 2] = (byte)(value >> 8);
        b[offset + 3] = (byte)(value >> 0);

        return 4;
    }

    public static int Write(this byte[] b, int offset, ArraySegment<byte> value)
    {
        Buffer.BlockCopy(value.Array, value.Offset, b, offset, value.Count);
        return value.Count;
    }

    public static ushort ReadBoolean(this byte[] b, int offset, out bool value)
    {
        value = b[offset] != 0;
        return 1;
    }

    public static ushort ReadUint8(this byte[] b, int offset, out byte value)
    {
        value = b[offset];
        return 1;
    }

    public static ushort ReadUint16BE(this byte[] b, int offset, out ushort value)
    {
        value = 0;
        value |= (ushort)(b[offset + 0] << 8);
        value |= (ushort)(b[offset + 1] << 0);

        return 2;
    }
    
    public static ushort ReadInt32BE(this byte[] b, int offset, out int value)
    {
        value = 0;
        value |= (b[offset + 0] << 24);
        value |= (b[offset + 1] << 16);
        value |= (b[offset + 2] << 8);
        value |= (b[offset + 3] << 0);
        
        return 4;
    }

    public static ushort ReadUint32BE(this byte[] b, int offset, out uint value)
    {
        value = 0;
        value |= (uint)(b[offset + 0] << 24);
        value |= (uint)(b[offset + 1] << 16);
        value |= (uint)(b[offset + 2] << 8);
        value |= (uint)(b[offset + 3] << 0);
        
        return 4;
    }
}