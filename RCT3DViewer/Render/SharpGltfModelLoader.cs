using System.Numerics;
using SharpGLTF.Schema2;
using StbImageSharp;

namespace RCT3D.Render;

internal sealed class SharpGltfModelLoader
{
    public ImportedModelData Load(string path, ModelImportSettings? settings = null)
    {
        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"glTF model not found: '{fullPath}'.", fullPath);

        settings ??= new ModelImportSettings();

        if (!float.IsFinite(settings.Scale) || settings.Scale <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Model import scale must be finite and greater than zero.");

        if (settings.TargetHeight is { } targetHeight && (!float.IsFinite(targetHeight) || targetHeight <= 0.0f))
            throw new ArgumentOutOfRangeException(nameof(settings), "Target model height must be finite and greater than zero.");

        try {
            return LoadModel(fullPath, settings);
        }
        catch (InvalidDataException) {
            throw;
        }
        catch (Exception exception) {
            throw new InvalidDataException($"Failed to load glTF model '{fullPath}'.", exception);
        }
    }

    private static ImportedModelData LoadModel(string path, ModelImportSettings settings)
    {
        var model = ModelRoot.Load(path);
        var scene = model.DefaultScene ?? throw new InvalidDataException("The glTF model has no default scene.");
        var meshes = new List<ImportedModelMesh>();
        var textureCache = new Dictionary<Image, ImportedModelTexture>();

        foreach (var node in scene.VisualChildren)
            AddNodeMeshes(node, meshes, textureCache, settings.Scale);

        if (meshes.Count == 0)
            throw new InvalidDataException("The glTF scene contains no triangle meshes.");

        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var mesh in meshes)
        foreach (var vertex in mesh.Vertices) {
            min = Vector3.Min(min, vertex.Position);
            max = Vector3.Max(max, vertex.Position);
        }

        if (settings.TargetHeight is { } height) {
            var sourceHeight = max.Y - min.Y;
            if (!float.IsFinite(sourceHeight) || sourceHeight <= 1e-6f)
                throw new InvalidDataException("Cannot apply target height to a model with zero or invalid vertical extent.");

            var scale = height / sourceHeight;
            for (var meshIndex = 0; meshIndex < meshes.Count; meshIndex++) {
                var mesh = meshes[meshIndex];
                var vertices = mesh.Vertices;
                for (var vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++) {
                    var vertex = vertices[vertexIndex];
                    vertex.Position *= scale;
                    vertices[vertexIndex] = vertex;
                }
                meshes[meshIndex] = mesh with { Vertices = vertices };
            }

            min *= scale;
            max *= scale;
        }

        return new ImportedModelData(meshes.ToArray(), min, max);
    }

    private static void AddNodeMeshes(
        Node node,
        List<ImportedModelMesh> meshes,
        Dictionary<Image, ImportedModelTexture> textureCache,
        float scale)
    {
        if (node.Mesh is { } nodeMesh) {
            var worldMatrix = node.WorldMatrix;
            if (!Matrix4x4.Invert(worldMatrix, out var inverseWorld))
                throw new InvalidDataException($"Node '{node.Name}' has a non-invertible transform.");

            var normalMatrix = Matrix4x4.Transpose(inverseWorld);
            var reversesWinding = worldMatrix.GetDeterminant() < 0.0f;

            foreach (var primitive in nodeMesh.Primitives) {
                if (primitive.DrawPrimitiveType != PrimitiveType.TRIANGLES)
                    throw new InvalidDataException($"Mesh primitive '{nodeMesh.Name}' is not a triangle list.");

                var positionAccessor = primitive.GetVertexAccessor("POSITION")
                    ?? throw new InvalidDataException($"Mesh primitive '{nodeMesh.Name}' has no POSITION attribute.");
                var sourcePositions = positionAccessor.AsVector3Array();
                if (sourcePositions.Count == 0)
                    throw new InvalidDataException($"Mesh primitive '{nodeMesh.Name}' contains no vertices.");

                var normalAccessor = primitive.GetVertexAccessor("NORMAL");
                var sourceNormals = normalAccessor?.AsVector3Array();
                if (sourceNormals is not null && sourceNormals.Count != sourcePositions.Count)
                    throw new InvalidDataException($"Mesh primitive '{nodeMesh.Name}' has mismatched POSITION and NORMAL counts.");

                var uvAccessor = primitive.GetVertexAccessor("TEXCOORD_0");
                var sourceUvs = uvAccessor?.AsVector2Array();
                if (sourceUvs is not null && sourceUvs.Count != sourcePositions.Count)
                    throw new InvalidDataException($"Mesh primitive '{nodeMesh.Name}' has mismatched POSITION and TEXCOORD_0 counts.");

                var rawIndices = primitive.GetIndices();
                var indices = rawIndices is null
                    ? Enumerable.Range(0, sourcePositions.Count).Select(index => (uint)index).ToArray()
                    : rawIndices.Select(index => {
                        if (index < sourcePositions.Count) return index;

                        throw new InvalidDataException($"Mesh primitive '{nodeMesh.Name}' has an index outside its vertex range.");
                    }).ToArray();

                if (indices.Length == 0 || indices.Length % 3 != 0)
                    throw new InvalidDataException($"Mesh primitive '{nodeMesh.Name}' has an invalid triangle index count.");

                if (reversesWinding) {
                    for (var index = 0; index < indices.Length; index += 3)
                        (indices[index + 1], indices[index + 2]) = (indices[index + 2], indices[index + 1]);
                }

                var vertices = new ImportedModelVertex[sourcePositions.Count];
                for (var index = 0; index < vertices.Length; index++) {
                    var position = Vector3.Transform(sourcePositions[index], worldMatrix) * scale;

                    var normal = sourceNormals is null
                        ? Vector3.Zero
                        : NormalizeOrZero(Vector3.TransformNormal(sourceNormals[index], normalMatrix));

                    var uv = sourceUvs is null ? Vector2.Zero : sourceUvs[index];
                    if (!IsFinite(position) || sourceNormals is not null && !IsFinite(normal) || !IsFinite(uv))
                        throw new InvalidDataException($"Mesh primitive '{nodeMesh.Name}' contains non-finite vertex data.");

                    vertices[index] = new ImportedModelVertex(position, normal, uv);
                }

                if (sourceNormals is null)
                    GenerateNormals(vertices, indices, nodeMesh.Name ?? "mesh");

                for (var index = 0; index < indices.Length; index += 3)
                    (indices[index + 1], indices[index + 2]) = (indices[index + 2], indices[index + 1]);

                var material = CreateMaterial(primitive.Material, textureCache);
                meshes.Add(new ImportedModelMesh(nodeMesh.Name ?? "mesh", vertices, indices, material));
            }
        }

        foreach (var child in node.VisualChildren)
            AddNodeMeshes(child, meshes, textureCache, scale);
    }

    private static ImportedModelMaterial CreateMaterial(Material? material, Dictionary<Image, ImportedModelTexture> textureCache)
    {
        if (material is null)
            return new ImportedModelMaterial(Vector4.One, Vector3.Zero, null, null, false);

        var baseColor = material.FindChannel("BaseColor");
        var emissive = material.FindChannel("Emissive");
        var emissiveFactor = emissive?.Parameter ?? Vector4.Zero;

        return new ImportedModelMaterial(
            baseColor?.Parameter ?? Vector4.One,
            new Vector3(emissiveFactor.X, emissiveFactor.Y, emissiveFactor.Z),
            LoadTexture(baseColor?.Texture, textureCache),
            LoadTexture(emissive?.Texture, textureCache),
            material.DoubleSided);
    }

    private static ImportedModelTexture? LoadTexture(Texture? texture, Dictionary<Image, ImportedModelTexture> textureCache)
    {
        var image = texture?.PrimaryImage;
        if (image is null)
            return null;
        if (textureCache.TryGetValue(image, out var cached))
            return cached;

        var encodedPixels = image.Content.Content.ToArray();
        if (encodedPixels.Length == 0)
            throw new InvalidDataException($"glTF image '{image.Name}' has no image data.");

        var decoded = ImageResult.FromMemory(encodedPixels, ColorComponents.RedGreenBlueAlpha);
        var importedTexture = new ImportedModelTexture(decoded.Width, decoded.Height, decoded.Data);
        textureCache.Add(image, importedTexture);
        return importedTexture;
    }

    private static void GenerateNormals(ImportedModelVertex[] vertices, uint[] indices, string meshName)
    {
        var normals = new Vector3[vertices.Length];
        for (var index = 0; index < indices.Length; index += 3) {
            var a = (int)indices[index];
            var b = (int)indices[index + 1];
            var c = (int)indices[index + 2];
            var faceNormal = Vector3.Cross(vertices[b].Position - vertices[a].Position, vertices[c].Position - vertices[a].Position);
            if (!IsFinite(faceNormal) || faceNormal.LengthSquared() <= 1e-20f)
                continue;

            normals[a] += faceNormal;
            normals[b] += faceNormal;
            normals[c] += faceNormal;
        }

        for (var index = 0; index < vertices.Length; index++) {
            var normal = NormalizeOrZero(normals[index]);
            if (normal == Vector3.Zero)
                throw new InvalidDataException($"Mesh primitive '{meshName}' has degenerate triangles and missing normals.");
            vertices[index].Normal = normal;
        }
    }

    private static Vector3 NormalizeOrZero(Vector3 value)
    {
        var lengthSquared = value.LengthSquared();
        return !IsFinite(value) || !float.IsFinite(lengthSquared) || lengthSquared <= 1e-20f
            ? Vector3.Zero
            : value / MathF.Sqrt(lengthSquared);
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
