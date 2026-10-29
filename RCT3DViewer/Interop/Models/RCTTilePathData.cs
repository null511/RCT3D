using System.Runtime.InteropServices;

namespace RCT3D.Interop.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RCTTilePathData
{
    public int TileX;
    public int TileY;
    public uint SurfaceIdHash;
    public uint RailingsIdHash;
    public byte BaseZ;
    public byte ClearanceHeight;
    public byte SlopeDirection;
    public byte Edges;
    public byte Corners;
    public byte Flags;
    public byte RailingsSupportType;
    public RCTPathSupportOrientation SupportOrientation;
    public uint AdditionIdHash;
    public byte AdditionFlags;

    public bool IsSloped => (Flags & 1) != 0;
    public bool IsQueue => (Flags & 2) != 0;
    public bool IsLampAddition => (AdditionFlags & 1) != 0;
    public bool AdditionIsBroken => (AdditionFlags & 2) != 0;
    public bool IsBenchAddition => (AdditionFlags & 4) != 0;
}
