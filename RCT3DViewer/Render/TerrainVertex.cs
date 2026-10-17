using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3DViewer.Render;

[StructLayout(LayoutKind.Sequential)]
public struct TerrainVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TextureCoordinate;
    public float MaterialId;

    public TerrainVertex(Vector3 pos, Vector3 norm, Vector2 textureCoordinate, uint materialId)
    {
        Position = pos;
        Normal = norm;
        TextureCoordinate = textureCoordinate;
        MaterialId = materialId;
    }
}
