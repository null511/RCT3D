using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks;

internal sealed class HalfLoopTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette) =>
        throw new InvalidOperationException("Half-loop geometry must be built from a complete piece.");

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var last = tracks[^1];
        var startHeading = TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin);
        var endHeading = TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationEnd);
        var startDirection = TrackCenterlineGeometry.GetTrackDirection(startHeading);
        var endDirection = TrackCenterlineGeometry.GetTrackDirection(endHeading);
        var halfTile = WorldUnits.TileSize * 0.5f;
        var startOffset = (startHeading & 4) != 0 ? Vector3.Zero : startDirection * halfTile;
        var endOffset = (endHeading & 4) != 0 ? Vector3.Zero : endDirection * halfTile;
        var start = TrackCenterlineGeometry.GetTileCenter(first) - startOffset;
        var end = TrackCenterlineGeometry.GetTileCenter(last) + endOffset;
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * WorldUnits.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * WorldUnits.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * WorldUnits.HeightPerBaseUnit;

        var isUp = PieceType is RCTTrackElementType.HalfLoopUp;
        var startTangent = isUp
            ? Vector3.Normalize(startDirection + Vector3.UnitY * Pitch.Slope25)
            : startDirection;
        var endTangent = isUp
            ? endDirection
            : Vector3.Normalize(endDirection - Vector3.UnitY * Pitch.Slope25);
        var preferredSide = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, startDirection));
        if (!isUp)
            preferredSide = -preferredSide;
        var controlLength = Vector3.Distance(start, end) * 0.4f;
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(
            start, end, startTangent, endTangent, controlLength,
            tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, palette,
            startTangent: startTangent,
            endTangent: endTangent,
            preferredSide: preferredSide);
    }
}
