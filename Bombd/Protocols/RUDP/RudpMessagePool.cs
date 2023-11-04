namespace Bombd.Protocols.RUDP;

public class RudpMessagePool
{
    private readonly Stack<RudpMessage> _messages = new();

    public RudpMessagePool(int capacity)
    {
        for (int i = 0; i < capacity; ++i)
            _messages.Push(new RudpMessage());
    }

    public RudpMessage Get() => _messages.Count > 0 ? _messages.Pop() : new RudpMessage();

    public void Return(RudpMessage message)
    {
        message.Reset();
        _messages.Push(message);
    }
}