using System.Text;

namespace Bombd.Serialization;

public class NetworkReader
{
    private ArraySegment<byte> _buffer;
    public int Offset;

    public NetworkReader(ArraySegment<byte> segment)
    {
        _buffer = segment;
    }

    public NetworkReader(string encoded)
    {
        _buffer = Convert.FromBase64String(encoded);
    }

    public int Capacity => _buffer.Count;

    public void SetBuffer(ArraySegment<byte> segment)
    {
        _buffer = segment;
        Offset = 0;
    }

    public void SetBuffer(string encoded)
    {
        _buffer = new ArraySegment<byte>(Convert.FromBase64String(encoded));
        Offset = 0;
    }

    public static T Deserialize<T>(ArraySegment<byte> data) where T : INetworkReadable, new()
    {
        using NetworkReaderPooled reader = NetworkReaderPool.Get(data);
        var serializable = new T();
        serializable.Read(reader);
        return serializable;
    }
    
    public static T Deserialize<T>(string encoded) where T : INetworkReadable, new()
    {
        using NetworkReaderPooled reader = NetworkReaderPool.Get(encoded);
        var serializable = new T();
        serializable.Read(reader);
        return serializable;
    }

    public static List<T> Deserialize<T>(string encoded, int count) where T : INetworkReadable, new()
    {
        using NetworkReaderPooled reader = NetworkReaderPool.Get(encoded);
        var arr = new List<T>(count);
        
        for (int i = 0; i < count; ++i)
        {
            var serializable = new T();
            serializable.Read(reader);
            arr.Add(serializable);
        }
        
        return arr;
    }

    public T Read<T>() where T : INetworkReadable, new()
    {
        var value = new T();
        value.Read(this);
        return value;
    }

    public ArraySegment<byte> ReadSegment(int size)
    {
        var segment = new ArraySegment<byte>(_buffer.Array!, _buffer.Offset + Offset, size);
        Offset += size;
        return segment;
    }

    public byte[] ReadBytes(int size)
    {
        byte[] buf = new byte[size];
        Buffer.BlockCopy(_buffer.Array!, _buffer.Offset + Offset, buf, 0, size);
        Offset += size;
        return buf;
    }

    public byte ReadInt8() => _buffer[Offset++];

    public short ReadInt16()
    {
        short value = 0;
        value |= (short)(_buffer[Offset + 0] << 8);
        value |= (short)(_buffer[Offset + 1] << 0);
        Offset += 2;
        return value;
    }

    public int ReadInt32()
    {
        int value = 0;
        value |= _buffer[Offset + 0] << 24;
        value |= _buffer[Offset + 1] << 16;
        value |= _buffer[Offset + 2] << 8;
        value |= _buffer[Offset + 3] << 0;
        Offset += 4;
        return value;
    }

    public ushort ReadUInt16() => (ushort)ReadInt16();
    public uint ReadUInt32() => (uint)ReadInt32();

    public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

    public string ReadString()
    {
        int len = ReadInt32();
        if (len > 0)
        {
            string value = Encoding.ASCII.GetString(_buffer.Array!, _buffer.Offset + Offset, len);
            Offset += len;
            return value;
        }

        return string.Empty;
    }

    public string ReadString(int size)
    {
        int start = Offset;
        int terminator = Offset;
        int end = Offset + size;

        while (terminator < end && _buffer[terminator] != 0)
            terminator++;

        Offset += size;

        if (start == terminator) return string.Empty;

        return Encoding.ASCII.GetString(_buffer.Array!, _buffer.Offset + start, terminator - start);
    }
}