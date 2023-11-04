namespace Bombd.Serialization;

public class NetworkWriterPooled : NetworkWriter, IDisposable
{
    internal NetworkWriterPooled()
    {
    }

    public void Dispose() => NetworkWriterPool.Return(this);
}