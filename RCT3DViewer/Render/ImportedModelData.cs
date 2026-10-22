using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render;

[StructLayout(LayoutKind.Sequential)]
public struct ImportedModelVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TextureCoordinate;

    public ImportedModelVertex(Vector3 position, Vector3 normal, Vector2 textureCoordinate)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = textureCoordinate;
    }
}

internal sealed record ImportedModelData(ImportedModelMesh[] Meshes, Vector3 BoundsMin, Vector3 BoundsMax);

internal sealed record ImportedModelMesh(
    string Name,
    ImportedModelVertex[] Vertices,
    uint[] Indices,
    ImportedModelMaterial Material);

internal sealed record ImportedModelMaterial(
    Vector4 BaseColorFactor,
    Vector3 EmissiveFactor,
    ImportedModelTexture? BaseColorTexture,
    ImportedModelTexture? EmissiveTexture,
    bool DoubleSided);

internal sealed record ImportedModelTexture(int Width, int Height, byte[] Pixels);

internal sealed record ModelImportSettings(float Scale = 1.0f, float? TargetHeight = null);
