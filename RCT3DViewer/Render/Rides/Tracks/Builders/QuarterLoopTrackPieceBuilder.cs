using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks.Builders;

internal sealed class QuarterLoopTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette) =>
        throw new InvalidOperationException("Quarter-loop geometry must be built from a complete piece.");

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var isUp = PieceType is RCTTrackElementType.Up90ToInvertedFlatQuarterLoop
            or RCTTrackElementType.MultiDimUp90ToInvertedFlatQuarterLoop
            or RCTTrackElementType.MultiDimInvertedUp90ToFlatQuarterLoop;
        var startDirection = TrackCenterlineGeometry.GetTrackDirection(
            TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin));
        var endDirection = TrackCenterlineGeometry.GetTrackDirection(
            TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationEnd));
        var halfTile = WorldUnits.TileSize * 0.5f;
        var start = TrackCenterlineGeometry.GetTileCenter(first);
        var end = TrackCenterlineGeometry.GetTileCenter(tracks[^1]);
        if (isUp)
            end += endDirection * halfTile;
        else {
            start -= startDirection * halfTile;
            // The final sequence is the virtual tile before the vertical endpoint.
            end += endDirection * WorldUnits.TileSize;
        }
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * WorldUnits.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * WorldUnits.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * WorldUnits.HeightPerBaseUnit;
        var startsInvertedFlat = PieceType is RCTTrackElementType.InvertedFlatToDown90QuarterLoop;
        if (isUp || startsInvertedFlat)
            start.Y += TrackCenterlineGeometry.VerticalInvertedAnchorOffset;
        end.Y += TrackCenterlineGeometry.VerticalInvertedAnchorOffset;

        var startTangent = isUp ? Vector3.UnitY : startDirection;
        var endTangent = isUp ? endDirection : -Vector3.UnitY;
        var preferredSide = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, startDirection));
        if (startsInvertedFlat)
            preferredSide = -preferredSide;
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(
            start, end, startTangent, endTangent, Vector3.Distance(start, end) * 0.4f,
            Math.Max(tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile, 32));

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, palette,
            startTangent: startTangent, endTangent: endTangent, preferredSide: preferredSide);
    }
}
