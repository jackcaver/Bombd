using System.Net;

namespace Bombd.Protocols;

public interface IServer
{
    bool IsActive { get; }
    EndPoint Endpoint { get; }
    string Name { get; }
    void Start();
    void Stop();
}