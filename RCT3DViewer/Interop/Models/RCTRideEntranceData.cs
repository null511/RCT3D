using System.Runtime.InteropServices;

namespace RCT3D.Interop.Models;

public enum RCTRideEntranceType : byte
{
    Entrance = 0,
    Exit = 1,
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RCTRideEntranceData
{
    public int TileX;
    public int TileY;
    public ushort RideId;
    public byte StationIndex;
    public RCTRideEntranceType EntranceType;
    public byte BaseHeight;
    public byte Direction;
}
