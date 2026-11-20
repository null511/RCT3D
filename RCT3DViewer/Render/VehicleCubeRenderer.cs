using System.Numerics;

namespace RCT3D.Render;

internal static class VehicleCubeRenderer
{
    public static ImportedModelMesh CreateMesh(Vector3 size)
    {
        var faces = new (Vector3 Normal, Vector3 A, Vector3 B, Vector3 C, Vector3 D)[] {
            (Vector3.UnitY, new(-0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f)),
            (-Vector3.UnitY, new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f)),
            (Vector3.UnitX, new(0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, -0.5f)),
            (-Vector3.UnitX, new(-0.5f, -0.5f, 0.5f), new(-0.5f, -0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, 0.5f)),
            (Vector3.UnitZ, new(-0.5f, -0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(0.5f, -0.5f, 0.5f)),
            (-Vector3.UnitZ, new(0.5f, -0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f)),
        };

        var vertices = new ImportedModelVertex[faces.Length * 4];
        var indices = new uint[faces.Length * 6];

        for (var faceIndex = 0; faceIndex < faces.Length; faceIndex++) {
            var face = faces[faceIndex];
            var vertexIndex = faceIndex * 4;

            vertices[vertexIndex] = new ImportedModelVertex(face.A * size, face.Normal, Vector2.Zero);
            vertices[vertexIndex + 1] = new ImportedModelVertex(face.B * size, face.Normal, Vector2.UnitX);
            vertices[vertexIndex + 2] = new ImportedModelVertex(face.C * size, face.Normal, Vector2.One);
            vertices[vertexIndex + 3] = new ImportedModelVertex(face.D * size, face.Normal, Vector2.UnitY);

            var index = faceIndex * 6;
            indices[index] = (uint)vertexIndex;
            indices[index + 1] = (uint)(vertexIndex + 1);
            indices[index + 2] = (uint)(vertexIndex + 2);
            indices[index + 3] = (uint)vertexIndex;
            indices[index + 4] = (uint)(vertexIndex + 2);
            indices[index + 5] = (uint)(vertexIndex + 3);
        }

        var material = new ImportedModelMaterial(
            Vector4.One,
            Vector3.Zero,
            new ImportedModelTexture(1, 1, [255, 255, 255, 255]),
            null,
            false);

        return new ImportedModelMesh("ride-vehicle-cube", vertices, indices, material);
    }

}
