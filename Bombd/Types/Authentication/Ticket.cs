using Bombd.Serialization;

namespace Bombd.Types.Authentication;

[Serializable]
public class Ticket : INetworkReadable
{
    public ulong AccountId;
    public string Domain;
    public DateTime ExpireDate;
    public DateTime IssuedDate;
    public int IssuerId;
    public string OnlineId;
    public byte[] Region;
    public byte[] SerialId;
    public string ServiceId;
    public int Status;

    public void Read(NetworkReader reader)
    {
        ushort major = reader.ReadUInt16();
        ushort minor = reader.ReadUInt16();
        int length = reader.ReadInt32();

        var userData = reader.Read<TicketBlob>();
        var signature = reader.Read<TicketBlob>();

        SerialId = userData.ReadParameter<byte[]>(TicketParameters.SerialId);
        IssuerId = userData.ReadParameter<int>(TicketParameters.IssuerId);
        IssuedDate = userData.ReadParameter<DateTime>(TicketParameters.IssuedDate);
        ExpireDate = userData.ReadParameter<DateTime>(TicketParameters.ExpireDate);
        AccountId = userData.ReadParameter<ulong>(TicketParameters.AccountId);
        OnlineId = userData.ReadParameter<string>(TicketParameters.OnlineId);
        Region = userData.ReadParameter<byte[]>(TicketParameters.Region);
        Domain = userData.ReadParameter<string>(TicketParameters.Domain);
        ServiceId = userData.ReadParameter<string>(TicketParameters.ServiceId);
        Status = userData.ReadParameter<int>(TicketParameters.Status);
    }
}