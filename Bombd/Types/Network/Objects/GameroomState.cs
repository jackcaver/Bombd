using Bombd.Helpers;
using Bombd.Serialization;

namespace Bombd.Types.Network.Objects;

public class GameroomState : INetworkWritable
{
    public Platform Platform;
    
    public RoomState State = RoomState.None;
    public bool HasEventVetoOccured;
    public bool IsLeaderVetoAvailable;
    public int LoadEventTime;
    public float LockedForRacerJoinsValue;
    public float LockedTimerValue;
    public int GameSessionUid = CryptoHelper.GetRandomSecret();
    
    public GameroomState(Platform platform) => Platform = platform;
    
    public void Write(NetworkWriter writer)
    {
        writer.Write((int)State);
        writer.Write(LoadEventTime);
        writer.Write(LockedForRacerJoinsValue);
        writer.Write(LockedTimerValue);
        writer.Write(IsLeaderVetoAvailable);
        writer.Write(HasEventVetoOccured);
        if (Platform == Platform.Karting) writer.Write(GameSessionUid);
        else writer.Clear(0x2);
    }
}