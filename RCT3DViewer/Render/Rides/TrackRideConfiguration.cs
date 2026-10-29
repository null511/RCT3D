using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Definitions;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides;

internal class TrackRideConfiguration(
    RCTRideType rideType,
    IReadOnlyList<ITrackPartGenerator> partGenerators,
    RideSupportStyle supportStyle,
    float centerlineHeightOffset,
    float supportTrackOffset,
    float carVerticalOffset,
    Action<RCTTrackElementData, List<TrackSupportPlacement>>? getSupportPlacements,
    bool smoothMovement,
    IStationBuilder? stationBuilder,
    float trackBankAngleDegrees)
{
    public RCTRideType RideType { get; } = rideType;
    public IReadOnlyList<ITrackPartGenerator> PartGenerators { get; } = partGenerators;
    public RideSupportStyle SupportStyle { get; } = supportStyle;
    public float CenterlineHeightOffset { get; } = centerlineHeightOffset;
    public float SupportTrackOffset { get; } = supportTrackOffset;
    public float CarVerticalOffset { get; } = carVerticalOffset;
    public bool SmoothMovement { get; } = smoothMovement;
    public float TrackBankAngleDegrees { get; } = trackBankAngleDegrees;
    public IStationBuilder? StationBuilder { get; } = stationBuilder;
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
    private static readonly TrackRideConfiguration[] configurations = [
        new BobsleighCoaster().Build(),
        new ClassicMiniRollerCoaster().Build(),
        new ClassicWoodenRollerCoaster().Build(),
        new CompactInvertedCoaster().Build(),
        new CorkscrewRollerCoaster().Build(),
        new GoKarts().Build(),
        new InvertedRollerCoaster().Build(),
        new LoopingRollerCoaster().Build(),
        new MineRide().Build(),
        new MineTrainCoaster().Build(),
        new MiniatureRailway().Build(),
        new Monorail().Build(),
        new WoodenRollerCoaster().Build(),
        new WoodenWildMouse().Build(),
    ];

    public static IReadOnlyList<TrackRideConfiguration> All => configurations;
}
