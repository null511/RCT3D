using RCT3D.Interop;
using System.Numerics;

namespace RCT3D.Render.Tracks.Meshing;

internal interface ITrackPieceMeshBuilder
{
    RCTTrackElementType PieceType { get; }
    bool RequiresCompletePiece { get; }

    void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette);
    void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette);
}

internal abstract class TrackPieceMeshBuilder(RCTTrackElementType pieceType) : ITrackPieceMeshBuilder
{
    public RCTTrackElementType PieceType { get; } = pieceType;
    public virtual bool RequiresCompletePiece => false;

    public abstract void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette);

    public virtual void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette)
    {
        foreach (ref readonly var track in tracks)
            Add(in track, geometry, partGenerators, palette);
    }
}

internal interface ITrackPartGenerator
{
    void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, ReadOnlySpan<Vector3> centerline, ReadOnlySpan<RCTPaletteEntry> palette, float rollBegin, float rollEnd, Vector3? startTangent, Vector3? endTangent, Vector3? preferredSide, bool smoothRollTransition);
}
