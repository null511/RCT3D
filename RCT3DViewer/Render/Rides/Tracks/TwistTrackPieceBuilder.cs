using System.Numerics;
using RCT3D.Interop.Models;

namespace RCT3D.Render.Rides.Tracks;

internal sealed class TwistTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    private const float HalfTwist = MathF.PI;

    public override bool RequiresCompletePiece => true;

    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries) =>
        throw new InvalidOperationException("Twist track geometry must be built from a complete piece.");

    public override void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> paletteEntries)
    {
        if (tracks.IsEmpty) return;

        var first = tracks[0];
        var last = tracks[^1];
        var startHeading = TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationBegin);
        var endHeading = TrackCenterlineGeometry.CombineDirection(first.Direction, first.RotationEnd);
        var startDirection = TrackCenterlineGeometry.GetTrackDirection(startHeading);
        var endDirection = TrackCenterlineGeometry.GetTrackDirection(endHeading);
        var halfTile = WorldUnits.TileSize * 0.5f;
        var start = TrackCenterlineGeometry.GetTileCenter(first) - startDirection * halfTile;
        var end = TrackCenterlineGeometry.GetTileCenter(last) + endDirection * halfTile;
        var startHeight = (first.BaseHeight - first.SequenceOffsetZ) * WorldUnits.HeightPerBaseUnit;
        var endHeight = (last.BaseHeight - last.SequenceOffsetZ) * WorldUnits.HeightPerBaseUnit;
        start.Y = startHeight + first.SplineZBegin * WorldUnits.HeightPerBaseUnit;
        end.Y = endHeight + last.SplineZEnd * WorldUnits.HeightPerBaseUnit;

        var centerline = TrackCenterlineGeometry.CreateCubicCurvePoints(
            start,
            end,
            startDirection,
            endDirection,
            Vector3.Distance(start, end) / 3.0f,
            tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile);
        var (startRoll, endRoll) = PieceType switch {
            RCTTrackElementType.LeftTwistDownToUp => (0.0f, HalfTwist),
            RCTTrackElementType.RightTwistUpToDown => (HalfTwist, 0.0f),
            _ => (0.0f, -HalfTwist),
        };

        TrackCenterlineGeometry.AddParts(
            first,
            geometry,
            centerline,
            partGenerators,
            paletteEntries,
            startTangent: startDirection,
            endTangent: endDirection,
            rollBegin: startRoll,
            rollEnd: endRoll,
            smoothRollTransition: true);
    }
}
