using RCT3D.Interop.Models;
using System.Numerics;
using System.Text.Json;

namespace RCT3D.Render.Rides;

internal static class RideVehicleDefinitions
{
    private static readonly RideVehicleCarDefinition defaultDefinition = new();
    private static Dictionary<(uint ObjectIdHash, byte VehicleType), RideVehicleCarDefinition> definitions = new();

    public static void Initialize(string openRCT2Path) =>
        definitions = CreateDefinitions(Path.Combine(openRCT2Path, "object"));

    public static RideVehicleCarDefinition Resolve(in RCTVehicleData vehicle) =>
        definitions.GetValueOrDefault((vehicle.ObjectIdHash, vehicle.VehicleType), defaultDefinition);


    private static Dictionary<(uint ObjectIdHash, byte VehicleType), RideVehicleCarDefinition> CreateDefinitions(string objectDirectory)
    {
        var result = new Dictionary<(uint ObjectIdHash, byte VehicleType), RideVehicleCarDefinition>();
        var objectIndex = RideVehicleObjectIndex.Load(objectDirectory);
        var definitionNames = new HashSet<string>(StringComparer.Ordinal);

        var directory = Path.Combine(AppContext.BaseDirectory, "Rides", "Vehicles");
        var files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToArray();

        if (files.Length == 0)
            throw new InvalidDataException($"No vehicle definitions found in '{directory}'.");

        foreach (var file in files) {
            try {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                var carsElement = document.RootElement.GetProperty("cars");

                if (carsElement.ValueKind != JsonValueKind.Array || carsElement.GetArrayLength() is 0 or > 256)
                    throw new InvalidDataException("'cars' must contain between 1 and 256 car definitions.");

                var name = Path.GetFileNameWithoutExtension(file);
                var key = RideVehicleObjectIndex.NormalizeName(name);
                if (!definitionNames.Add(key))
                    throw new InvalidDataException($"Duplicate normalized vehicle filename '{name}'.");

                if (!objectIndex.TryGetValue(key, out var identifiers) || identifiers.Count == 0)
                    throw new InvalidDataException($"No ride object matches '{name}' in '{objectDirectory}'. "
                        + "Name this JSON after the object's English name (en-GB, or en-US if absent), ignoring spaces and punctuation.");

                var cars = carsElement.EnumerateArray().Select(ReadCar).ToArray();
                foreach (var identifier in identifiers)
                    Add(identifier, cars);
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException
                or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) {
                throw new InvalidDataException($"Invalid vehicle definition '{file}': {exception.Message}", exception);
            }
        }

        return result;

        void Add(string? identifier, RideVehicleCarDefinition[] cars) {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new InvalidDataException("Object identifiers must not be empty.");

            var objectHash = MaterialLibraryBuilder.HashObjectId(identifier);

            for (var index = 0; index < cars.Length; index++) {
                if (!result.TryAdd((objectHash, checked((byte)index)), cars[index]))
                    throw new InvalidDataException($"Duplicate vehicle mapping for '{identifier}' (hash {objectHash:X8}), car type {index}.");
            }
        }
    }

    private static RideVehicleCarDefinition ReadCar(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Each car definition must be an object.");

        var size = RideVehicleCarDefinition.DefaultSize;

        if (element.TryGetProperty("size", out var sizeElement)) {
            if (sizeElement.ValueKind != JsonValueKind.Array || sizeElement.GetArrayLength() != 3)
                throw new InvalidDataException("Car size must contain three numbers.");

            size = new Vector3(sizeElement[0].GetSingle(), sizeElement[1].GetSingle(), sizeElement[2].GetSingle());
            if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || !float.IsFinite(size.Z)
                || size.X <= 0 || size.Y <= 0 || size.Z <= 0)
                throw new InvalidDataException("Car size must be finite and greater than zero.");
        }

        return new RideVehicleCarDefinition {
            Size = size,
            LocalOffset = ReadOffset("localOffset", defaultDefinition.LocalOffset),
            WorldOffset = ReadOffset("worldOffset", defaultDefinition.WorldOffset),
            CameraOffset = ReadOffset("cameraOffset", defaultDefinition.CameraOffset),
            PaintStyle = element.TryGetProperty("paintStyle", out var paintStyle) ? paintStyle.GetByte() : null,
        };

        float ReadOffset(string name, float fallback) {
            var value = element.TryGetProperty(name, out var property) ? property.GetSingle() : fallback;
            if (float.IsFinite(value)) return value;

            throw new InvalidDataException($"Car '{name}' must be finite.");
        }
    }
}
