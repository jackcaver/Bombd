namespace Bombd.Serialization;

public class NetworkReaderPooled : NetworkReader, IDisposable
{
    internal NetworkReaderPooled(ArraySegment<byte> segment) : base(segment)
    {
    }

    public void Dispose() => NetworkReaderPool.Return(this);
}