using RCT3D.Render.Rides;
using Serilog;

namespace RCT3D.Render;

internal sealed record VehicleModel(RideVehicleCarDefinition Definition, ImportedModelData Data, bool IsFallback)
{
    public static VehicleModel Load(RideVehicleCarDefinition definition)
    {
        if (definition.Model is { } reference) {
            string? path = null;
            try {
                var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "Vehicle");
                if (string.IsNullOrWhiteSpace(reference) || Path.IsPathRooted(reference))
                    throw new InvalidDataException("Vehicle model must specify a nonempty relative path.");

                path = Path.GetFullPath(Path.Combine(directory, reference));
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (!path.StartsWith(directory + Path.DirectorySeparatorChar, comparison))
                    throw new InvalidDataException("Vehicle model path must remain inside Assets\\Models\\Vehicle.");

                var extension = Path.GetExtension(path);
                if (!extension.Equals(".glb", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Vehicle models must use the .glb or .gltf format.");

                var data = new SharpGltfModelLoader().Load(path, new ModelImportSettings(Scale: definition.ModelScale));
                return new VehicleModel(definition, data, false);
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException) {
                Log.Error(exception, "Failed to load vehicle model {Model} at {ModelPath}; using its fallback cube", reference, path);
            }
        }

        var mesh = VehicleCubeRenderer.CreateMesh(definition.Size);
        var halfSize = definition.Size * 0.5f;
        return new VehicleModel(definition, new ImportedModelData([mesh], -halfSize, halfSize), true);
    }
}