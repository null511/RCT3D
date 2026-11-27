using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Paths.Meshing;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct PathSurfaceVertex
{
    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Vector2 TextureCoordinate;
    public readonly uint DescriptorIndex;
    public readonly uint Layer;

    public PathSurfaceVertex(Vector3 position, Vector3 normal, Vector2 texCoord, uint descriptorIndex, uint layer)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = texCoord;
        DescriptorIndex = descriptorIndex;
        Layer = layer;
    }
}
