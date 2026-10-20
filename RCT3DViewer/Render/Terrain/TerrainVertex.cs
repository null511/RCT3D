using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3DViewer.Render.Terrain;

[StructLayout(LayoutKind.Sequential)]
public struct TerrainVertex(Vector3 pos, Vector3 norm, Vector2 textureCoordinate, uint materialId)
{
    public Vector3 Position = pos;
    public Vector3 Normal = norm;
    public Vector2 TextureCoordinate = textureCoordinate;
    public float MaterialId = materialId;
}
