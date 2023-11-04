namespace Bombd.Serialization;

public static class NetworkReaderPool
{
    private const int PoolSize = 128;
    private static readonly Stack<NetworkReaderPooled> Pool = new(PoolSize);

    static NetworkReaderPool()
    {
        for (int i = 0; i < PoolSize; ++i)
            Pool.Push(new NetworkReaderPooled(ArraySegment<byte>.Empty));
    }

    private static NetworkReaderPooled Get() =>
        Pool.Count > 0 ? Pool.Pop() : new NetworkReaderPooled(ArraySegment<byte>.Empty);

    public static NetworkReaderPooled Get(byte[] buffer)
    {
        NetworkReaderPooled reader = Get();
        reader.SetBuffer(buffer);
        return reader;
    }

    public static NetworkReaderPooled Get(string encoded)
    {
        NetworkReaderPooled reader = Get();
        reader.SetBuffer(encoded);
        return reader;
    }

    public static NetworkReaderPooled Get(ArraySegment<byte> segment)
    {
        NetworkReaderPooled reader = Get();
        reader.SetBuffer(segment);
        return reader;
    }

    public static void Return(NetworkReaderPooled reader)
    {
        Pool.Push(reader);
    }
}