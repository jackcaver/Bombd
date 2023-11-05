using Bombd.Helpers;
using Bombd.Types.Network;
using Bombd.Types.Network.Messages;

namespace Bombd.Simulation;

public class SyncObject
{
    public readonly string OwnerName;
    public readonly string DebugTag;
    public readonly int OwnerUserId;
    public readonly int Guid;
    public readonly int Type;
    public ArraySegment<byte> Data = ArraySegment<byte>.Empty;

    public SyncObject(string tag, int type)
    {
        OwnerUserId = -1;
        DebugTag = tag;
        Type = type;
        OwnerName = NetworkMessages.SimServerName;
        Guid = CryptoHelper.GetRandomSecret();
    }
    
    public SyncObject(NetMessageSyncObject message, int owner)
    {
        OwnerUserId = owner;
        OwnerName = message.OwnerName;
        DebugTag = message.DebugTag;
        Guid = message.Guid;
        Type = message.ObjectType;
    }

    public SyncObject(NetMessageSyncObjectCreate message, int owner)
    {
        OwnerUserId = owner;
        OwnerName = message.OwnerName;
        DebugTag = message.DebugTag;
        Guid = message.Guid;
        Type = message.ObjectType;
    }
    
    public override string ToString()
    {
        return $"SyncObject({Guid}:{DebugTag})";
    }
}