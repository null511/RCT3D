using RCT3D.Interop;
using RCT3D.Render.Rides.Definitions;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides;

internal class TrackRideConfiguration(
    RCTRideType rideType,
    TrackPieceBuilderRegistry pieceBuilders,
    IReadOnlyList<ITrackPartGenerator> partGenerators,
    RideSupportStyle supportStyle,
    float centerlineHeightOffset,
    float supportTrackOffset,
    Action<RCTTrackElementData, List<TrackSupportPlacement>>? getSupportPlacements)
{
    private readonly Dictionary<RCTTrackElementType, ITrackPieceMeshBuilder> builders = pieceBuilders.Builders.ToDictionary(builder => builder.PieceType);

    public RCTRideType RideType { get; } = rideType;
    public IReadOnlyList<ITrackPartGenerator> PartGenerators { get; } = partGenerators;
    public RideSupportStyle SupportStyle { get; } = supportStyle;
    public float CenterlineHeightOffset { get; } = centerlineHeightOffset;
    public float SupportTrackOffset { get; } = supportTrackOffset;
    public IRideSupportMeshBuilder? SupportMeshBuilder { get; } = RideSupportMeshBuilders.Resolve(supportStyle);
    public Action<RCTTrackElementData, List<TrackSupportPlacement>>? GetSupportPlacements { get; } = getSupportPlacements;

    public bool TryResolvePiece(RCTTrackElementType type, out ITrackPieceMeshBuilder builder) => builders.TryGetValue(type, out builder!);
}

internal static class TrackRideConfigurations
{
    private static readonly TrackRideConfiguration[] configurations = [
        new Monorail().Build(),
        new MiniatureRailway().Build(),
        new WoodenWildMouse().Build(),
        new ClassicMiniRollerCoaster().Build(),
        new LoopingRollerCoaster().Build(),
        new CompactInvertedCoaster().Build(),
    ];

    public static IReadOnlyList<TrackRideConfiguration> All => configurations;
}
