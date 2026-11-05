using System.Runtime.InteropServices;

namespace RCT3D.Interop.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RCTTileObstacleData
{
    public int TileX;
    public int TileY;
    public byte BaseHeight;
    public byte ClearanceHeight;
}
