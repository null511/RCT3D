using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks.Builders;

internal sealed class BarrelRollTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette) =>
        throw new InvalidOperationException("Barrel-roll geometry must be built from a complete piece.");

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var direction = TrackCenterlineGeometry.GetTrackDirection(
            TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin));
        var halfTile = WorldUnits.TileSize * 0.5f;
        var start = TrackCenterlineGeometry.GetTileCenter(first) - direction * halfTile;
        var end = TrackCenterlineGeometry.GetTileCenter(tracks[^1]) + direction * halfTile;
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * WorldUnits.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * WorldUnits.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * WorldUnits.HeightPerBaseUnit;

        var isUpToDown = PieceType is RCTTrackElementType.LeftBarrelRollUpToDown
            or RCTTrackElementType.RightBarrelRollUpToDown;
        var signedHalfTwist = PieceType is RCTTrackElementType.LeftBarrelRollUpToDown
            or RCTTrackElementType.LeftBarrelRollDownToUp ? MathF.PI : -MathF.PI;
        var rollBegin = isUpToDown ? 0.0f : -signedHalfTwist;
        var rollEnd = isUpToDown ? signedHalfTwist : 0.0f;
        if (isUpToDown)
            end.Y += TrackCenterlineGeometry.VerticalInvertedAnchorOffset;
        else
            start.Y += TrackCenterlineGeometry.VerticalInvertedAnchorOffset;

        var subdivisions = Math.Max(tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile, 32);
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(
            start, end, direction, direction, Vector3.Distance(start, end) / 3.0f, subdivisions);
        // Native barrel movement bows seven map units sideways, reversing side for DownToUp.
        var lateralOffset = Vector3.Cross(Vector3.UnitY, direction)
            * (7.0f / 32.0f * WorldUnits.TileSize)
            * MathF.Sign(signedHalfTwist) * (isUpToDown ? 1.0f : -1.0f);
        for (var i = 1; i < subdivisions; i++) {
            var bow = MathF.Sin(MathF.PI * i / subdivisions);
            points[i] += lateralOffset * (bow * bow);
        }

        TrackCenterlineGeometry.AddParts(first, geometry, points, partGenerators, palette,
            rollBegin, rollEnd, startTangent: direction, endTangent: direction,
            smoothRollTransition: true);
    }
}
