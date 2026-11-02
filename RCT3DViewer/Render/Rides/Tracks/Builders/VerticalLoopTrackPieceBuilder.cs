using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks.Builders;

internal sealed class VerticalLoopTrackPieceBuilder(RCTTrackElementType pieceType) : TrackPieceMeshBuilder(pieceType)
{
    private const float SlopedSectionLength = 0.2f;

    public override bool RequiresCompletePiece => true;


    public override void Add(in RCTTrackElementData track, TrackMeshGeometry geometry, IReadOnlyList<ITrackPartGenerator> partGenerators, ReadOnlySpan<RCTPaletteEntry> palette) =>
        throw new InvalidOperationException("Vertical loop geometry must be built from a complete piece.");

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

        var loopHeightBaseUnits = 0;
        foreach (ref readonly var track in tracks)
            loopHeightBaseUnits = Math.Max(loopHeightBaseUnits, track.SequenceOffsetZ);

        if (loopHeightBaseUnits <= 0)
            throw new InvalidDataException($"Vertical loop {PieceType} has no positive sequence height.");

        var loopHeight = (loopHeightBaseUnits + 2.0f) * WorldUnits.HeightPerBaseUnit;
        var horizontalRadius = loopHeightBaseUnits * WorldUnits.HeightPerBaseUnit
            / (2.0f * (1.2f + Pitch.Slope25));
        var totalWidth = horizontalRadius * 2.0f;
        var totalHeight = loopHeight;
        var midpointHeight = totalHeight - 1.2f*horizontalRadius;

        var horizontalDirection = Vector3.Normalize(startDirection + endDirection);
        var preferredSide = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, horizontalDirection));
        var startTangent = Vector3.Normalize(startDirection + Vector3.UnitY * Pitch.Slope25);
        var endTangent = Vector3.Normalize(endDirection - Vector3.UnitY * Pitch.Slope25);
        var loopStart = start + startTangent * SlopedSectionLength;
        var loopEnd = end - endTangent * SlopedSectionLength;
        var slopeSubdivisions = Math.Max(1, (int)MathF.Ceiling(
            SlopedSectionLength / WorldUnits.TileSize * TrackCenterlineGeometry.CurveSubdivisionsPerTile));
        var curveSubdivisions = Math.Max(4, tracks.Length * TrackCenterlineGeometry.CurveSubdivisionsPerTile / 4);
        var loopCenter = (loopStart + loopEnd) * 0.5f;
        var lowerEntryAnchor = loopCenter
            + horizontalDirection * (totalWidth * 0.5f)
            + Vector3.UnitY * midpointHeight;
        var apex = loopCenter + Vector3.UnitY * totalHeight;
        var lowerExitAnchor = loopCenter
            - horizontalDirection * (totalWidth * 0.5f)
            + Vector3.UnitY * midpointHeight;
        var lowerEntry = TrackCenterlineGeometry.CreateCubicCurvePoints(
            loopStart, lowerEntryAnchor, startTangent, Vector3.UnitY,
            Vector3.Distance(loopStart, lowerEntryAnchor) * 0.4f, curveSubdivisions);
        var upperEntry = TrackCenterlineGeometry.CreateCubicCurvePoints(
            lowerEntryAnchor, apex, Vector3.UnitY, -horizontalDirection,
            Vector3.Distance(lowerEntryAnchor, apex) * 0.4f, curveSubdivisions);
        var upperExit = TrackCenterlineGeometry.CreateCubicCurvePoints(
            apex, lowerExitAnchor, -horizontalDirection, -Vector3.UnitY,
            Vector3.Distance(apex, lowerExitAnchor) * 0.4f, curveSubdivisions);
        var lowerExit = TrackCenterlineGeometry.CreateCubicCurvePoints(
            lowerExitAnchor, loopEnd, -Vector3.UnitY, endTangent,
            Vector3.Distance(lowerExitAnchor, loopEnd) * 0.4f, curveSubdivisions);
        var centerline = new List<Vector3>(2 * slopeSubdivisions + 4 * curveSubdivisions + 1);

        for (var i = 0; i <= slopeSubdivisions; i++)
            centerline.Add(Vector3.Lerp(start, loopStart, i / (float)slopeSubdivisions));

        AppendCurve(lowerEntry);
        AppendCurve(upperEntry);
        AppendCurve(upperExit);
        AppendCurve(lowerExit);

        for (var i = 1; i <= slopeSubdivisions; i++)
            centerline.Add(Vector3.Lerp(loopEnd, end, i / (float)slopeSubdivisions));

        TrackCenterlineGeometry.AddParts(first, geometry, centerline.ToArray(), partGenerators, palette,
            startTangent: startTangent,
            endTangent: endTangent,
            preferredSide: preferredSide);

        void AppendCurve(Vector3[] curve)
        {
            for (var i = 1; i < curve.Length; i++)
                centerline.Add(curve[i]);
        }
    }
}