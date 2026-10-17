using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3DViewer.Render;

[StructLayout(LayoutKind.Sequential)]
public struct TerrainVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector3 Color;

    public TerrainVertex(Vector3 pos, Vector3 norm, Vector3 col)
    {
        Position = pos;
        Normal = norm;
        Color = col;
    }
}
