using Bombd.Helpers;

namespace Bombd.Types.Network;

public class NetObjectTypeInfo
{
    public NetObjectTypeInfo(string name, int id)
    {
        TypeName = name;
        KartingTypeId = CryptoHelper.StringHash32(name);
        ModnationTypeId = id;
    }
        
    public string TypeName;
    public int KartingTypeId;
    public int ModnationTypeId;
}