using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks.Builders;

internal sealed class VerticalTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette) =>
        throw new InvalidOperationException("Vertical track geometry must be built from a complete piece.");

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (tracks.IsEmpty || tracks[0].Sequence != 0) return;

        var first = tracks[0];
        var (isUp, startVertical, endVertical) = PieceType switch {
            RCTTrackElementType.Up90 => (true, true, true),
            RCTTrackElementType.Down90 => (false, true, true),
            RCTTrackElementType.Up60ToUp90 => (true, false, true),
            RCTTrackElementType.Down60ToDown90 => (false, false, true),
            RCTTrackElementType.Up90ToUp60 => (true, true, false),
            RCTTrackElementType.Down90ToDown60 => (false, true, false),
            _ => throw new InvalidOperationException($"Unsupported vertical track piece: {PieceType}."),
        };
        var direction = TrackCenterlineGeometry.GetTrackDirection(
            TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin));
        var center = TrackCenterlineGeometry.GetTileCenter(first);
        var halfTile = WorldUnits.TileSize * 0.5f;
        var start = startVertical ? center : center - direction * halfTile;
        var end = endVertical ? center : center + direction * halfTile;
        var originHeight = first.BaseHeight - first.SequenceOffsetZ;
        start.Y = (originHeight + first.SplineZBegin) * WorldUnits.HeightPerBaseUnit;
        end.Y = (originHeight + first.SplineZEnd) * WorldUnits.HeightPerBaseUnit;

        var verticalDirection = isUp ? Vector3.UnitY : -Vector3.UnitY;
        var slopeDirection = Vector3.Normalize(direction + verticalDirection * Pitch.Slope60);
        var startTangent = startVertical ? verticalDirection : slopeDirection;
        var endTangent = endVertical ? verticalDirection : slopeDirection;
        var controlLength = Vector3.Distance(start, end) * 0.4f;
        if (startVertical)
            start.Y += TrackCenterlineGeometry.VerticalInvertedAnchorOffset;
        if (endVertical)
            end.Y += TrackCenterlineGeometry.VerticalInvertedAnchorOffset;
        var points = startVertical && endVertical
            ? new[] { start, end }
            : TrackCenterlineGeometry.CreateCubicCurvePoints(
                start, end, startTangent, endTangent, controlLength,
                TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, palette,
            TrackCenterlineGeometry.GetRollRadians(first.RollBegin, geometry.TrackBankAngleDegrees),
            TrackCenterlineGeometry.GetRollRadians(first.RollEnd, geometry.TrackBankAngleDegrees),
            startTangent: startTangent,
            endTangent: endTangent,
            preferredSide: Vector3.Normalize(Vector3.Cross(Vector3.UnitY, direction)));
    }
}
