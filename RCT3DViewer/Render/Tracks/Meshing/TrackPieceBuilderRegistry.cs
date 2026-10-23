using RCT3D.Interop;

namespace RCT3D.Render.Tracks.Meshing;

internal sealed class TrackPieceBuilderRegistry
{
    private readonly Dictionary<RCTTrackElementType, ITrackPieceMeshBuilder> builders = new();

    public void Register(ITrackPieceMeshBuilder builder) => builders.Add(builder.PieceType, builder);

    public void RegisterMany<TBuilder>(Func<RCTTrackElementType, TBuilder> createBuilder, params RCTTrackElementType[] pieceTypes)
        where TBuilder : ITrackPieceMeshBuilder
    {
        foreach (var pieceType in pieceTypes)
            Register(createBuilder(pieceType));
    }

    public IEnumerable<ITrackPieceMeshBuilder> Builders => builders.Values;
}
