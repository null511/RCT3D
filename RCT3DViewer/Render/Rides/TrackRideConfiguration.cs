using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;
using System.Reflection;

namespace RCT3D.Render.Rides;

internal class TrackRideConfiguration(
    RCTRideType rideType,
    IReadOnlyList<ITrackPartGenerator> partGenerators,
    RideSupportStyle supportStyle,
    float centerlineHeightOffset,
    float centerlineVerticalOffset,
    float supportTrackOffset,
    RideVehicleDefinition vehicles,
    Action<RCTTrackElementData, List<TrackSupportPlacement>>? getSupportPlacements,
    bool smoothMovement,
    ITrackStationBuilder? stationBuilder,
    float trackBankAngleDegrees,
    RidePlatformSurfaceType platformSurface = RidePlatformSurfaceType.Wood,
    RidePlatformRailingType platformRailing = RidePlatformRailingType.Chained,
    Func<RCTTrackElementData, Vector2[]?>? getWaterPlane = null,
    IDirectTrackMeshBuilder? directMeshBuilder = null)
{
    public RCTRideType RideType { get; } = rideType;
    public IDirectTrackMeshBuilder? DirectMeshBuilder { get; } = directMeshBuilder;
    public IReadOnlyList<ITrackPartGenerator> PartGenerators { get; } = partGenerators;
    public RideSupportStyle SupportStyle { get; } = supportStyle;
    public float CenterlineHeightOffset { get; } = centerlineHeightOffset;
    public float CenterlineVerticalOffset { get; } = centerlineVerticalOffset;
    public float SupportTrackOffset { get; } = supportTrackOffset;
    public float CarLocalOffset { get; } = vehicles.LocalOffset;
    public float CarWorldOffset { get; } = vehicles.WorldOffset;
    public Vector3 VehicleSize { get; } = vehicles.Size;
    public bool SmoothMovement { get; } = smoothMovement;
    public float TrackBankAngleDegrees { get; } = trackBankAngleDegrees;
    public ITrackStationBuilder? StationBuilder { get; } = stationBuilder;
    public RidePlatformSurfaceType PlatformSurface { get; } = platformSurface;
    public RidePlatformRailingType PlatformRailing { get; } = platformRailing;
    public Func<RCTTrackElementData, Vector2[]?>? GetWaterPlane { get; } = getWaterPlane;
    public IRideSupportMeshBuilder? SupportMeshBuilder { get; } = RideSupportMeshBuilders.Resolve(supportStyle);
    public Action<RCTTrackElementData, List<TrackSupportPlacement>>? GetSupportPlacements { get; } = getSupportPlacements;

    public bool HasSupportPlacements => SupportMeshBuilder is { } builder
        && (builder.PlacementMode == TrackSupportPlacementMode.TrackBased
            || builder.HasDefaultSupportPlacements
            || GetSupportPlacements is not null);

    public void PopulateSupportPlacements(RCTTrackElementData track, List<TrackSupportPlacement> placements)
    {
        if (SupportMeshBuilder is { HasDefaultSupportPlacements: true } builder) {
            builder.GetDefaultSupportPlacements(track, placements);
        }
        else if (GetSupportPlacements is { } getSupportPlacements) {
            getSupportPlacements(track, placements);
        }
        else {
            placements.Clear();
        }
    }

}

internal static class TrackRideConfigurations
{
    private static readonly TrackRideConfiguration[] configurations = CreateConfigurations();

    public static IReadOnlyList<TrackRideConfiguration> All => configurations;

    private static TrackRideConfiguration[] CreateConfigurations()
    {
        var definitionTypes = typeof(RideDefinition).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(RideDefinition).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        var configurationList = definitionTypes.Select(CreateConfiguration).ToArray();

        var duplicate = configurationList
            .GroupBy(configuration => configuration.RideType)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
            throw new InvalidOperationException($"Multiple ride definitions found for ride type '{duplicate.Key}'.");

        return configurationList;
    }

    private static TrackRideConfiguration CreateConfiguration(Type definitionType)
    {
        var constructor = definitionType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);

        if (constructor is null)
            throw new InvalidOperationException($"Ride definition '{definitionType.FullName}' must have a parameterless constructor.");

        if (constructor.Invoke(null) is not RideDefinition definition)
            throw new InvalidOperationException($"Could not create ride definition '{definitionType.FullName}'.");

        return definition.Build();
    }
}
