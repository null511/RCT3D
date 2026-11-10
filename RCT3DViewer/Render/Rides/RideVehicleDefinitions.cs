using RCT3D.Interop.Models;
using System.Reflection;

namespace RCT3D.Render.Rides;

internal static class RideVehicleDefinitions
{
    private static readonly RideVehicleCarDefinition defaultDefinition = new();
    private static readonly Dictionary<(uint ObjectIdHash, byte VehicleType), RideVehicleCarDefinition> definitions = CreateDefinitions();

    public static RideVehicleCarDefinition Resolve(in RCTVehicleData vehicle) =>
        definitions.GetValueOrDefault((vehicle.ObjectIdHash, vehicle.VehicleType), defaultDefinition);


    private static Dictionary<(uint ObjectIdHash, byte VehicleType), RideVehicleCarDefinition> CreateDefinitions()
    {
        var result = new Dictionary<(uint ObjectIdHash, byte VehicleType), RideVehicleCarDefinition>();

        var definitionTypes = typeof(RideVehicleDefinition).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(RideVehicleDefinition).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var definitionType in definitionTypes) {
            var mappings = definitionType.GetCustomAttributes<RideVehicleMappingAttribute>(inherit: false)
                .OrderBy(mapping => mapping.Identifier, StringComparer.Ordinal)
                .ThenBy(mapping => mapping.LegacyIdentifier, StringComparer.Ordinal)
                .ToArray();

            if (mappings.Length == 0) continue;

            var constructor = definitionType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                Type.EmptyTypes,
                modifiers: null);

            if (constructor is null)
                throw new InvalidOperationException($"Vehicle definition '{definitionType.FullName}' must have a parameterless constructor.");

            foreach (var mapping in mappings) {
                if (constructor.Invoke(null) is not RideVehicleDefinition train)
                    throw new InvalidOperationException($"Could not create vehicle definition '{definitionType.FullName}'.");

                if (train.Cars is null || train.Cars.Length is 0 or > 256)
                    throw new InvalidOperationException($"Vehicle definition '{definitionType.FullName}' must define between 1 and 256 cars.");

                Add(mapping.Identifier, train, definitionType);

                if (mapping.LegacyIdentifier is not null)
                    Add(mapping.LegacyIdentifier, train, definitionType);
            }
        }

        //Add("rct2.ride.ctcar", "0A188A80|CTCAR   |2082E6E1", new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //Add("rct2.ride.rcr", "0A188B80|RCR     |14407591", new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //Add("rct2.ride.spcar", "0A188B80|SPCAR   |69F23BC4", new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //Add("rct2.ride.truck1", "0A188B80|TRUCK1  |58B728C5", new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //// Legacy descriptors format the checksum with space padding, not leading zeroes.
        //Add("rct2.ride.vcr", "0A188C80|VCR     | 4C462E7", new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //Add("rct1.ride.racing_cars", null, new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //Add("rct1.ride.sports_cars", null, new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });
        //Add("rct1aa.ride.vintage_cars", null, new() { LocalOffset = 0.2f }, new() { HelperPaintStyle = 1 });

        return result;

        void Add(string identifier, RideVehicleDefinition train, Type definitionType) {
            var objectHash = MaterialLibraryBuilder.HashObjectId(identifier);

            for (var index = 0; index < train.Cars.Length; index++) {
                var car = train.Cars[index];

                if (car is null)
                    throw new InvalidOperationException($"Vehicle definition '{definitionType.FullName}' has no car definition at index {index}.");

                if (!result.TryAdd((objectHash, checked((byte)index)), car))
                    throw new InvalidOperationException($"Duplicate vehicle mapping for '{identifier}' (hash {objectHash:X8}), car type {index}, in '{definitionType.FullName}'.");
            }
        }
    }
}
