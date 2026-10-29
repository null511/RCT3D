using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render;

[StructLayout(LayoutKind.Sequential)]
internal struct ModelVertex(Vector3 position, Vector3 normal, Vector2 textureCoordinate)
{
    public Vector3 Position = position;
    public Vector3 Normal = normal;
    public Vector2 TextureCoordinate = textureCoordinate;
}
