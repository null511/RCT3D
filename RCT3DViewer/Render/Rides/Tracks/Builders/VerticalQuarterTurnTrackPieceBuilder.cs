using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks.Builders;

internal sealed class VerticalQuarterTurnTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette) =>
        throw new InvalidOperationException("Vertical quarter-turn geometry must be built from a complete piece.");

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var startDirection = TrackCenterlineGeometry.GetTrackDirection(
            TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin));
        var endDirection = TrackCenterlineGeometry.GetTrackDirection(
            TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationEnd));
        var start = TrackCenterlineGeometry.GetTileCenter(first);
        var end = start;
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * WorldUnits.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * WorldUnits.HeightPerBaseUnit
            + TrackCenterlineGeometry.VerticalInvertedAnchorOffset;
        end.Y = originHeight + first.SplineZEnd * WorldUnits.HeightPerBaseUnit
            + TrackCenterlineGeometry.VerticalInvertedAnchorOffset;
        var tangent = Vector3.Normalize(end - start);
        var startSide = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, startDirection));
        var endSide = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, endDirection));
        var rollEnd = MathF.Atan2(Vector3.Dot(tangent, Vector3.Cross(startSide, endSide)),
            Vector3.Dot(startSide, endSide));
        var subdivisions = Math.Max(tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile, 32);
        var points = new Vector3[subdivisions + 1];
        for (var i = 0; i <= subdivisions; i++)
            points[i] = Vector3.Lerp(start, end, i / (float)subdivisions);

        TrackCenterlineGeometry.AddVerticallyAlignedParts(first, geometry, points, partGenerators, palette,
            startVertical: true, endVertical: true,
            rollEnd: rollEnd, startTangent: tangent, endTangent: tangent, preferredSide: startSide,
            smoothRollTransition: true);
    }
}
