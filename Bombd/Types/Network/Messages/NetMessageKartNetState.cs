using Bombd.Helpers;
using Bombd.Types.Network.Data;

namespace Bombd.Types.Network.Messages;

public struct NetMessageKartNetState(Platform platform)
{
    // xA ??? (NetMatrix44 mLocalWorld)
        // int rotation?
        // short x
        // short y
        // short z?
    // ushort ??? [0.0, 1.0]
    // x4 ???
    // ushort mVelX [-100,100]
    // ushort mVelY; [-100,100]
    // ushort mVelZ [-100,100]
    // ushort mAccelX [-100,100]
    // ushort mAccelY [-100,100]
    // ushort mAccelZ [-100,100]
    // byte x [-3,3]
    // byte y [-3,3]
    // byte z [-3, 3]
    // x1 Padding
    // ulong Flags
    public int LocalTime;
    public uint PlayerNameUid;
}