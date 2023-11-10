using Bombd.Serialization;

namespace Bombd.Simulation;

public class PlayerInfo : INetworkWritable, INetworkReadable
{
    public int Operation;
    public int NetcodeUserId;
    public int NetcodeGamePlayerId;
    public int PlayerConnectId;
    public int GuestOfPlayerNameUid;
    public bool IsGroupLeader;
    public int PlayerGroupId;
    public string NameUid;
    public string PlayerName;
    public string PodLocation;
    
    public void Read(NetworkReader reader)
    {
        Operation = reader.ReadInt32();
        NetcodeUserId = reader.ReadInt32();
        NetcodeGamePlayerId = reader.ReadInt32();
        PlayerConnectId = reader.ReadInt32();
        GuestOfPlayerNameUid = reader.ReadInt32();
        IsGroupLeader = reader.ReadInt8() == 1;
        PlayerGroupId = reader.ReadInt32();
        NameUid = reader.ReadString();
        PlayerName = reader.ReadString();
        PodLocation = reader.ReadString();
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(Operation);
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