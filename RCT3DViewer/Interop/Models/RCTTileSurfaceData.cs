using System.Runtime.InteropServices;

namespace RCT3D.Interop.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RCTTileSurfaceData
{
    public uint TerrainSurfaceIdHash;
    public uint TerrainEdgeIdHash;
    public byte BaseZ;
    public byte SlopeFlags;
    public byte NorthCornerHeight;
    public byte EastCornerHeight;
    public byte SouthCornerHeight;
    public byte WestCornerHeight;
    public ushort WaterHeight;
    public byte GrassState;
    public byte CanGrowGrass;
    public byte Reserved0;
    public byte Reserved1;
}
