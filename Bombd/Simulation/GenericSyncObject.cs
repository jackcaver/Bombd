using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Simulation;

public class GenericSyncObject<T> : SyncObject where T : INetworkWritable
{

    private T _value;
    
    public T Value
    {
        get => _value;
        set
        {
            _value = value;
            Sync();
        }
    }

    public Action OnUpdate;
    
    public GenericSyncObject(T instance, int type, string owner, int userId) : base(typeof(T).Name, type, owner, userId)
    {
        Data = NetworkWriter.Serialize(instance);
        _value = instance;
    }
    
    public void Sync()
    {
        Data = NetworkWriter.Serialize(Value);
        OnUpdate();
    }
    
    public static implicit operator T(GenericSyncObject<T> value) => value.Value;
}