using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Serialization;
using Bombd.Types.Network.Simulation;

namespace Bombd.Types.Network.Messages;

public struct NetMessagePlayerUpdate(Platform platform) : INetworkWritable, INetworkReadable
{
    public ICollection<PlayerState> Data = [];
    
    public static NetMessagePlayerUpdate ReadVersioned(ArraySegment<byte> data, Platform platform)
    {
        var msg = new NetMessagePlayerUpdate(platform);
        using var reader = NetworkReaderPool.Get(data);
        msg.Read(reader);
        return msg;
    }
    
    public void Read(NetworkReader reader)
    {
        if (platform == Platform.Karting)
        {
            int count = reader.ReadInt32();
            Data = new List<PlayerState>(count);
            for (int i = 0; i < count; ++i)
            {
                var state = new PlayerState
                {
                    NameUid = reader.ReadUInt32(),
                    Away = (reader.ReadBool() ? 1 : 0),
                    Mic = (reader.ReadBool() ? 1 : 0),
                    HasEventVetoed = reader.ReadBool(),
                    HasLeaderVetoed = reader.ReadBool(),
                    IsConnecting = reader.ReadBool(),
                    KartHandlingDrift = reader.ReadSingle(),
                    KartSpeedAccel = reader.ReadSingle(),
                    KartId = reader.ReadInt32(),
                    CharacterId = reader.ReadInt32()
                };
            
                Data.Add(state);
            }
        }
        else
        {
            ArraySegment<byte> xml = reader.ReadSegment(0x320);
            Data = new List<PlayerState>
            {
                PlayerState.LoadXml(xml)
            };
        }
    }
    
    public void Write(NetworkWriter writer)
    {
        writer.Write(Data.Count);
        if (platform == Platform.Karting)
        {
            foreach (PlayerState data in Data)
            {
                writer.Write(data.NameUid);
                writer.Write(data.Away != 0);
                writer.Write(data.Mic != 0);
                writer.Write(data.HasEventVetoed);
                writer.Write(data.HasLeaderVetoed);
                writer.Write(data.IsConnecting);
                writer.Write(data.KartHandlingDrift);
                writer.Write(data.KartSpeedAccel);
                writer.Write(data.KartId);
                writer.Write(data.CharacterId);
            }
        }
        else
        {
            foreach (PlayerState data in Data)
            {
                writer.Write(data.NetcodeUserId);
                writer.Write(data.PlayerConnectId);
                writer.Write(data.Away != 0);
                writer.Write(data.Mic != 0);
                writer.Write(data.HasEventVetoed);
                writer.Write(data.HasLeaderVetoed);
                writer.Write(data.IsConnecting);
                writer.Clear(0x3);
                writer.Write(data.KartHandlingDrift);
                writer.Write(data.KartSpeedAccel);
            }
        }
    }
}