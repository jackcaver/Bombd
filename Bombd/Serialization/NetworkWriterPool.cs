namespace Bombd.Serialization;

public static class NetworkWriterPool
{
    private const int PoolSize = 128;
    private static readonly Stack<NetworkWriterPooled> Pool = new(PoolSize);
    private static readonly object AcquireLock = new();

    static NetworkWriterPool()
    {
        for (int i = 0; i < PoolSize; ++i)
            Pool.Push(new NetworkWriterPooled());
    }

    public static NetworkWriterPooled Get()
    {
        lock (AcquireLock)
        {
            return Pool.Count > 0 ? Pool.Pop() : new NetworkWriterPooled();
        }
    }

    public static void Return(NetworkWriterPooled writer)
    {
        lock (AcquireLock)
        {
            writer.Reset();
            Pool.Push(writer);
        }
    }
}