using RCT3D.Interop;
using RCT3D.Render.Tracks.Rides;

namespace RCT3D.Render.Tracks.Meshing;

internal class TrackRideConfiguration(
    RCTRideType rideType,
    TrackPieceBuilderRegistry pieceBuilders,
    IReadOnlyList<ITrackPartGenerator> partGenerators,
    RideSupportStyle supportStyle,
    ITrackSupportPlacementMap? supportPlacementMap)
{
    private readonly Dictionary<RCTTrackElementType, ITrackPieceMeshBuilder> builders = pieceBuilders.Builders.ToDictionary(builder => builder.PieceType);

    public RCTRideType RideType { get; } = rideType;
    public IReadOnlyList<ITrackPartGenerator> PartGenerators { get; } = partGenerators;
    public RideSupportStyle SupportStyle { get; } = supportStyle;
    public IRideSupportMeshBuilder? SupportMeshBuilder { get; } = RideSupportMeshBuilders.Resolve(supportStyle);
    public ITrackSupportPlacementMap? SupportPlacementMap { get; } = supportPlacementMap;

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
    ];

    public static IReadOnlyList<TrackRideConfiguration> All => configurations;
}
