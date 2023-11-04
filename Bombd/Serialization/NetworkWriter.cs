using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Bombd.Serialization;

public class NetworkWriter
{
    private const int DefaultCapacity = 1024;

    private byte[] _buffer = new byte[DefaultCapacity];
    public int Offset;
    public int Capacity => _buffer.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Offset = 0;
    }

    public static ArraySegment<byte> Serialize(INetworkWritable writable)
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        writer.Write(writable);
        ArraySegment<byte> data = writer.ToArraySegment();
        byte[] array = new byte[data.Count];
        data.CopyTo(array);
        return new ArraySegment<byte>(array);
    }

    public static ArraySegment<byte> Serialize<T>(List<T> writables) where T : INetworkWritable
    {
        using NetworkWriterPooled writer = NetworkWriterPool.Get();
        foreach (T writable in writables) writer.Write(writable);
        ArraySegment<byte> data = writer.ToArraySegment();
        byte[] array = new byte[data.Count];
        data.CopyTo(array);
        return new ArraySegment<byte>(array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int value)
    {
        if (_buffer.Length >= value) return;
        int capacity = Math.Max(value, _buffer.Length * 2);
        Array.Resize(ref _buffer, capacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArraySegment<byte> ToArraySegment() => new(_buffer, 0, Offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(int size)
    {
        EnsureCapacity(Offset + size);
        new Span<byte>(_buffer, Offset, size).Clear();
        Offset += size;
    }

    public void Write(ArraySegment<byte> value)
    {
        EnsureCapacity(Offset + value.Count);
        value.CopyTo(_buffer, Offset);
        Offset += value.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(byte value)
    {
        EnsureCapacity(Offset + 1);
        _buffer[Offset++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(bool value)
    {
        EnsureCapacity(Offset + 1);
        _buffer[Offset++] = (byte)(value ? 1 : 0);
    }

    public void Write(short value)
    {
        const int size = sizeof(short);
        EnsureCapacity(Offset + size);
        var span = new Span<byte>(_buffer, Offset, size);
        BinaryPrimitives.WriteInt16BigEndian(span, value);
        Offset += size;
    }

    public void Write(int value)
    {
        const int size = sizeof(int);
        EnsureCapacity(Offset + size);
        var span = new Span<byte>(_buffer, Offset, size);
        BinaryPrimitives.WriteInt32BigEndian(span, value);
        Offset += size;
    }

    public void Write(long value)
    {
        const int size = sizeof(long);
        EnsureCapacity(Offset + size);
        var span = new Span<byte>(_buffer, Offset, size);
        BinaryPrimitives.WriteInt64BigEndian(span, value);
        Offset += size;
    }

    public void Write(ushort value)
    {
        const int size = sizeof(ushort);
        EnsureCapacity(Offset + size);
        var span = new Span<byte>(_buffer, Offset, size);
        BinaryPrimitives.WriteUInt16BigEndian(span, value);
        Offset += size;
    }

    public void Write(uint value)
    {
        const int size = sizeof(uint);
        EnsureCapacity(Offset + size);
        var span = new Span<byte>(_buffer, Offset, size);
        BinaryPrimitives.WriteUInt32BigEndian(span, value);
        Offset += size;
    }

    public void Write(ulong value)
    {
        const int size = sizeof(ulong);
        EnsureCapacity(Offset + size);
        var span = new Span<byte>(_buffer, Offset, size);
        BinaryPrimitives.WriteUInt64BigEndian(span, value);
        Offset += size;
    }

    public void Write(float value)
    {
        const int size = sizeof(float);
        EnsureCapacity(Offset + size);
        var span = new Span<byte>(_buffer, Offset, size);
        BinaryPrimitives.WriteSingleBigEndian(span, value);
        Offset += size;
    }

    public void Write(double value)
    {
        const int size = sizeof(double);
        EnsureCapacity(Offset + size);
        var span = new Span<byte>(_buffer, Offset, size);
        BinaryPrimitives.WriteDoubleBigEndian(span, value);
        Offset += size;
    }

    public void Write(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            Write(0);
            return;
        }

        int size = value.Length;
        EnsureCapacity(Offset + 4 + size + 1);
        Encoding.ASCII.GetBytes(value, 0, size, _buffer, Offset + 4);
        Write(size + 1);
        Offset += size;
        _buffer[Offset++] = 0;
    }

    public void Write(string value, int size)
    {
        EnsureCapacity(Offset + size);

        if (string.IsNullOrEmpty(value))
        {
            Clear(size);
            // _buffer[Offset] = 0;
            // Offset += size;
            return;
        }

        int len = Math.Min(value.Length, size - 1);
        int written = Encoding.ASCII.GetBytes(value, 0, len, _buffer, Offset);
        Offset += written;
        Clear(size - written);

        // _buffer[Offset + written] = 0;
        // Offset += size;
    }

    public void Write(INetworkWritable writable)
    {
        ArgumentNullException.ThrowIfNull(writable);
        writable.Write(this);
    }
}