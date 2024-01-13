using Bombd.Serialization;
using Bombd.Types.Network;
using Bombd.Types.Network.Room;

namespace Bombd.Simulation;

public class PlayerInfo : INetworkWritable, INetworkReadable
{
    public GameJoinStatus Operation = GameJoinStatus.Pending;
    public int NetcodeUserId;
    public int NetcodeGamePlayerId;
    public int PlayerConnectId;
    public uint GuestOfPlayerNameUid;
    public bool IsGroupLeader;
    public int PlayerGroupId;
    public string NameUid = string.Empty;
    public string PlayerName = string.Empty;
    public string PodLocation = string.Empty;

    public void Update(PlayerInfo info)
    {
        Operation = info.Operation;
        NetcodeUserId = info.NetcodeUserId;
        NetcodeGamePlayerId = info.NetcodeGamePlayerId;
        PlayerConnectId = info.PlayerConnectId;
        GuestOfPlayerNameUid = info.GuestOfPlayerNameUid;
        IsGroupLeader = info.IsGroupLeader;
        PlayerGroupId = info.PlayerGroupId;
        NameUid = info.NameUid;
        PlayerName = info.PlayerName;
        PodLocation = info.PodLocation;
    }
    
    public void Read(NetworkReader reader)
    {
        Operation = (GameJoinStatus)reader.ReadInt32();
        NetcodeUserId = reader.ReadInt32();
        NetcodeGamePlayerId = reader.ReadInt32();
        PlayerConnectId = reader.ReadInt32();
        GuestOfPlayerNameUid = reader.ReadUInt32();
        IsGroupLeader = reader.ReadInt8() == 1;
        PlayerGroupId = reader.ReadInt32();
        NameUid = reader.ReadString();
        PlayerName = reader.ReadString();
        PodLocation = reader.ReadString();
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write((int)Operation);
        writer.Write(NetcodeUserId);
        writer.Write(NetcodeGamePlayerId);
        writer.Write(PlayerConnectId);
        writer.Write(GuestOfPlayerNameUid);
        writer.Write(IsGroupLeader);
        writer.Write(PlayerGroupId);
        writer.Write(NameUid);
        writer.Write(PlayerName);
        writer.Write(PodLocation);
    }
}