using RCT3D.Interop;
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
    Action<RCTTrackElementData, List<TrackSupportPlacement>>? getSupportPlacements)
{
    public RCTRideType RideType { get; } = rideType;
    public IReadOnlyList<ITrackPartGenerator> PartGenerators { get; } = partGenerators;
    public RideSupportStyle SupportStyle { get; } = supportStyle;
    public float CenterlineHeightOffset { get; } = centerlineHeightOffset;
    public float SupportTrackOffset { get; } = supportTrackOffset;
    public IRideSupportMeshBuilder? SupportMeshBuilder { get; } = RideSupportMeshBuilders.Resolve(supportStyle);
    public Action<RCTTrackElementData, List<TrackSupportPlacement>>? GetSupportPlacements { get; } = getSupportPlacements;

    public bool HasSupportPlacements => SupportMeshBuilder is { } builder
        && (builder.HasDefaultSupportPlacements || GetSupportPlacements is not null);

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
        new Monorail().Build(),
        new MiniatureRailway().Build(),
        new WoodenWildMouse().Build(),
        new ClassicMiniRollerCoaster().Build(),
        new MineTrainCoaster().Build(),
        new ClassicWoodenRollerCoaster().Build(),
        new LoopingRollerCoaster().Build(),
        new CompactInvertedCoaster().Build(),
    ];

    public static IReadOnlyList<TrackRideConfiguration> All => configurations;
}
