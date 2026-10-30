using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render;

[StructLayout(LayoutKind.Sequential)]
public struct ImportedModelVertex(Vector3 position, Vector3 normal, Vector2 textureCoordinate)
{
    public Vector3 Position = position;
    public Vector3 Normal = normal;
    public Vector2 TextureCoordinate = textureCoordinate;
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
    bool DoubleSided,
    ImportedModelTexture? NormalTexture = null,
    ImportedModelTexture? OcclusionTexture = null,
    ImportedModelTexture? MetallicRoughnessTexture = null,
    ImportedModelTexture? DisplacementTexture = null,
    float MetallicFactor = 1.0f,
    float RoughnessFactor = 1.0f,
    float NormalScale = 1.0f,
    float OcclusionStrength = 1.0f);

internal sealed record ImportedModelTexture(int Width, int Height, byte[] Pixels, object? SourceIdentity = null);

internal sealed record ModelImportSettings(float Scale = 1.0f, float? TargetHeight = null);
