using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks.Builders;

internal sealed class VerticalSlopeTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette) =>
        throw new InvalidOperationException("Vertical slope geometry must be built from a complete piece.");

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (tracks.IsEmpty || tracks[0].Sequence != 0) return;

        var first = tracks[0];
        var last = tracks[^1];
        var isUp = PieceType == RCTTrackElementType.ReverseFreefallSlope;
        var startVertical = !isUp;
        var endVertical = isUp;
        var startDirection = TrackCenterlineGeometry.GetTrackDirection(
            TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin));
        var endDirection = TrackCenterlineGeometry.GetTrackDirection(
            TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationEnd));
        var halfTile = WorldUnits.TileSize * 0.5f;
        var start = TrackCenterlineGeometry.GetTileCenter(first);
        var end = TrackCenterlineGeometry.GetTileCenter(last);
        if (!startVertical) start -= startDirection * halfTile;
        if (!endVertical) end += endDirection * halfTile;

        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * WorldUnits.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * WorldUnits.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * WorldUnits.HeightPerBaseUnit;

        var verticalDirection = isUp ? Vector3.UnitY : -Vector3.UnitY;
        var startTangent = startVertical ? verticalDirection : startDirection;
        var endTangent = endVertical ? verticalDirection : endDirection;
        if (startVertical)
            start.Y += TrackCenterlineGeometry.VerticalInvertedAnchorOffset;
        if (endVertical)
            end.Y += TrackCenterlineGeometry.VerticalInvertedAnchorOffset;

        var preferredSide = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, startDirection));

        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(
            start, end, startTangent, endTangent, Vector3.Distance(start, end) * 0.4f,
            tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);

        TrackCenterlineGeometry.AddVerticallyAlignedParts(first, geometry, points, partGenerators, palette,
            startVertical, endVertical,
            startTangent: startTangent,
            endTangent: endTangent,
            preferredSide: preferredSide);
    }
}

internal sealed class AirThrustTopCapTrackPieceBuilder() : TrackPieceMeshBuilder(RCTTrackElementType.AirThrustTopCap)
{
    private const float NativeTileSize = 32f;
    private const float MovementSampleEndpointHeight = 1f;
    private const float MovementSamplePeakHeight = 47f;

    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette) =>
        throw new InvalidOperationException("Air-thrust top-cap geometry must be built from a complete piece.");

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (tracks.IsEmpty || tracks[0].Sequence != 0) return;

        var first = tracks[0];
        var startTrack = first;
        var endTrack = first;
        var hasStartTrack = false;
        var hasEndTrack = false;
        foreach (ref readonly var candidate in tracks) {
            if (candidate.Sequence == 1) {
                startTrack = candidate;
                hasStartTrack = true;
            }
            else if (candidate.Sequence == 2) {
                endTrack = candidate;
                hasEndTrack = true;
            }
        }

        if (!hasStartTrack || !hasEndTrack) return;

        var start = TrackCenterlineGeometry.GetTileCenter(startTrack);
        var end = TrackCenterlineGeometry.GetTileCenter(endTrack);
        var originHeight = (first.BaseHeight - first.SequenceOffsetZ) * WorldUnits.HeightPerBaseUnit;
        start.Y = originHeight + first.SplineZBegin * WorldUnits.HeightPerBaseUnit;
        end.Y = originHeight + first.SplineZEnd * WorldUnits.HeightPerBaseUnit;

        var startTangent = Vector3.UnitY;
        var endTangent = -Vector3.UnitY;
        start.Y += TrackCenterlineGeometry.VerticalInvertedAnchorOffset;
        end.Y += TrackCenterlineGeometry.VerticalInvertedAnchorOffset;
        var direction = TrackCenterlineGeometry.GetTrackDirection(
            TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin));
        var preferredSide = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, direction));
        var controlLength = (MovementSamplePeakHeight - MovementSampleEndpointHeight)
            * (WorldUnits.TileSize / NativeTileSize) * (4f / 3f);
        var points = TrackCenterlineGeometry.CreateCubicCurvePoints(
            start, end, startTangent, endTangent, controlLength,
            Math.Max(tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile, 32));

        TrackCenterlineGeometry.AddVerticallyAlignedParts(first, geometry, points, partGenerators, palette,
            startVertical: true, endVertical: true,
            startTangent: startTangent,
            endTangent: endTangent,
            preferredSide: preferredSide);
    }
}