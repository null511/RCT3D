using RCT3D.Interop;
using RCT3D.Render.Tracks.Rides;

namespace RCT3D.Render.Tracks.Meshing;

internal class TrackRideConfiguration(
    RCTRideType rideType,
    TrackPieceBuilderRegistry pieceBuilders,
    IReadOnlyList<ITrackPartGenerator> partGenerators)
{
    private readonly Dictionary<RCTTrackElementType, ITrackPieceMeshBuilder> builders = pieceBuilders.Builders.ToDictionary(builder => builder.PieceType);

    public RCTRideType RideType { get; } = rideType;
    public IReadOnlyList<ITrackPartGenerator> PartGenerators { get; } = partGenerators;

    public bool TryResolvePiece(RCTTrackElementType type, out ITrackPieceMeshBuilder builder) => builders.TryGetValue(type, out builder!);

    //public IReadOnlyCollection<ITrackPieceMeshBuilder> PieceBuilders => builders.Values;
}

internal static class TrackRideConfigurations
{
    private static readonly TrackRideConfiguration[] Configurations = [
        Monorail.Build(),
        MiniatureRailway.Build(),
    ];

    public static IReadOnlyList<TrackRideConfiguration> All => Configurations;
}
