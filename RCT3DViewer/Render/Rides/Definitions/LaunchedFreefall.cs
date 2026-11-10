using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class LaunchedFreefall : RideDefinition
{
    public LaunchedFreefall() : base(RCTRideType.LaunchedFreefall)
    {
        DirectMeshBuilder = new LaunchedFreefallTowerMeshBuilder();
    }
}

internal sealed class LaunchedFreefallTowerMeshBuilder : IDirectTrackMeshBuilder
{
    private const int SectionBaseHeight = 4;
    private const int BaseSectionCount = 3;
    private const float SectionHeight = SectionBaseHeight * WorldUnits.HeightPerBaseUnit;

    // Visual estimates from sprites 14564-14566, not the painter's sorting bounds.
    private const float FrameHalfWidth = 7f * WorldUnits.NativeUnit;
    private const float PostHalfWidth = 1.5f * WorldUnits.NativeUnit;
    private const float BraceHalfWidth = WorldUnits.NativeUnit;
    private const float ColumnRadius = 4f * WorldUnits.NativeUnit;
    private const float CapHeight = 8f * WorldUnits.NativeUnit;
    private const int ColumnSides = 12;

    private static readonly TrackRailSpecularData SteelSpecular = new(115, 150);

    public bool Supports(RCTTrackElementType part) =>
        part is RCTTrackElementType.TowerBase or RCTTrackElementType.TowerSection;

    public void AddPiece(
        ReadOnlySpan<RCTTrackElementData> tracks,
        IReadOnlySet<TrackPieceLocation> pieceLocations,
        TrackMeshGeometry geometry,
        ReadOnlySpan<RCTPaletteEntry> palette)
    {
        if (tracks.IsEmpty) return;

        var track = tracks[0];
        var hasOrigin = false;
        foreach (ref readonly var candidate in tracks) {
            if (candidate.Sequence != 0) continue;
            track = candidate;
            hasOrigin = true;
            break;
        }

        if (!hasOrigin || !Supports(track.Part)) return;

        var sectionCount = track.Part == RCTTrackElementType.TowerBase ? BaseSectionCount : 1;
        var nextLocation = new TrackPieceLocation(
            track.RideId, track.RideType, RCTTrackElementType.TowerSection,
            track.TileX, track.TileY, track.BaseHeight + sectionCount * SectionBaseHeight);
        var terminal = !pieceLocations.Contains(nextLocation);
        var origin = TrackCenterlineGeometry.GetTileCenter(track);
        origin.Y = track.BaseHeight * WorldUnits.HeightPerBaseUnit;
        var mainColor = TrackMeshPartGenerator.GetPaletteColor(track.MainPaletteIndex, palette);
        var columnColor = TrackMeshPartGenerator.GetPaletteColor(track.AdditionalPaletteIndex, palette);

        Vector3[] corners = [
            new(-FrameHalfWidth, 0, -FrameHalfWidth),
            new(FrameHalfWidth, 0, -FrameHalfWidth),
            new(FrameHalfWidth, 0, FrameHalfWidth),
            new(-FrameHalfWidth, 0, FrameHalfWidth),
        ];

        for (var section = 0; section < sectionCount; section++) {
            var bottom = section * SectionHeight;
            var top = bottom + SectionHeight;
            var capped = terminal && section == sectionCount - 1;
            var frameTop = capped ? top - CapHeight : top;
            var collarBottom = track.Part == RCTTrackElementType.TowerBase && section == 0
                ? bottom + 3f * WorldUnits.NativeUnit
                : bottom;

            foreach (var corner in corners)
                AddBeam(corner + Vector3.UnitY * bottom, corner + Vector3.UnitY * frameTop, PostHalfWidth);

            AddRing(collarBottom + PostHalfWidth);
            var middle = (collarBottom + frameTop) * 0.5f;
            AddRing(middle);

            for (var side = 0; side < corners.Length; side++) {
                var left = corners[side];
                var right = corners[(side + 1) % corners.Length];
                var inset = Vector3.Normalize(right - left) * PostHalfWidth;
                left += inset;
                right -= inset;

                AddCrossBraces(collarBottom + 2f * PostHalfWidth, middle - PostHalfWidth);
                AddCrossBraces(middle + PostHalfWidth, frameTop - 2f * PostHalfWidth);

                void AddCrossBraces(float low, float high) {
                    AddBeam(left + Vector3.UnitY * low, right + Vector3.UnitY * high, BraceHalfWidth);
                    AddBeam(right + Vector3.UnitY * low, left + Vector3.UnitY * high, BraceHalfWidth);
                }
            }

            AddColumn(bottom, frameTop, capped);
            if (!capped) continue;

            AddRing(frameTop - PostHalfWidth);
            foreach (var corner in corners) {
                var topCorner = corner + Vector3.UnitY * frameTop;

                AddQuad(
                    topCorner + new Vector3(-PostHalfWidth, 0, -PostHalfWidth),
                    topCorner + new Vector3(-PostHalfWidth, 0, PostHalfWidth),
                    topCorner + new Vector3(PostHalfWidth, 0, PostHalfWidth),
                    topCorner + new Vector3(PostHalfWidth, 0, -PostHalfWidth),
                    Vector3.UnitY, mainColor);

                AddBeam(topCorner, Vector3.UnitY * (top - PostHalfWidth), PostHalfWidth);
            }

            AddBeam(Vector3.UnitY * (top - 2f * PostHalfWidth), Vector3.UnitY * top, PostHalfWidth);

            AddQuad(
                new Vector3(-PostHalfWidth, top, -PostHalfWidth),
                new Vector3(-PostHalfWidth, top, PostHalfWidth),
                new Vector3(PostHalfWidth, top, PostHalfWidth),
                new Vector3(PostHalfWidth, top, -PostHalfWidth),
                Vector3.UnitY, mainColor);
        }

        void AddRing(float height) {
            for (var i = 0; i < corners.Length; i++) {
                var start = corners[i];
                var end = corners[(i + 1) % corners.Length];
                var inset = Vector3.Normalize(end - start) * PostHalfWidth;
                AddBeam(start + inset + Vector3.UnitY * height, end - inset + Vector3.UnitY * height, PostHalfWidth);
            }
        }

        void AddColumn(float bottom, float top, bool closeTop) {
            for (var i = 0; i < ColumnSides; i++) {
                var angle = i * MathF.Tau / ColumnSides;
                var nextAngle = (i + 1) * MathF.Tau / ColumnSides;
                var start = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
                var end = new Vector3(MathF.Cos(nextAngle), 0, MathF.Sin(nextAngle));
                var a = start * ColumnRadius + Vector3.UnitY * bottom;
                var b = start * ColumnRadius + Vector3.UnitY * top;
                var c = end * ColumnRadius + Vector3.UnitY * top;
                var d = end * ColumnRadius + Vector3.UnitY * bottom;
                AddQuad(a, b, c, d, Vector3.Normalize(start + end), columnColor);
                if (!closeTop) continue;

                var centerIndex = AddVertex(Vector3.UnitY * top, Vector3.UnitY, columnColor);
                var startIndex = AddVertex(b, Vector3.UnitY, columnColor);
                var endIndex = AddVertex(c, Vector3.UnitY, columnColor);
                geometry.AddIndexedTriangle(centerIndex, startIndex, endIndex);
            }
        }

        // Beam ends meet inside posts; leaving them open also avoids internal faces at shaft joins.
        void AddBeam(Vector3 start, Vector3 end, float halfWidth) {
            var tangent = Vector3.Normalize(end - start);
            var side = MathF.Abs(tangent.Y) > 0.99f
                ? Vector3.UnitX
                : Vector3.Normalize(Vector3.Cross(Vector3.UnitY, tangent));

            var up = Vector3.Normalize(Vector3.Cross(tangent, side));
            var a = start - side * halfWidth - up * halfWidth;
            var b = start + side * halfWidth - up * halfWidth;
            var c = start + side * halfWidth + up * halfWidth;
            var d = start - side * halfWidth + up * halfWidth;
            var offset = end - start;

            AddQuad(a, b, b + offset, a + offset, -up, mainColor);
            AddQuad(b, c, c + offset, b + offset, side, mainColor);
            AddQuad(c, d, d + offset, c + offset, up, mainColor);
            AddQuad(d, a, a + offset, d + offset, -side, mainColor);
        }

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, TrackRgba8 color) {
            var first = AddVertex(a, normal, color);
            var second = AddVertex(b, normal, color);
            var third = AddVertex(c, normal, color);
            var fourth = AddVertex(d, normal, color);

            geometry.AddIndexedTriangle(first, third, second);
            geometry.AddIndexedTriangle(first, fourth, third);
        }

        uint AddVertex(Vector3 position, Vector3 normal, TrackRgba8 color) =>
            geometry.AddIndexedVertex(origin + Rotate(position), Rotate(normal), color, SteelSpecular);

        Vector3 Rotate(Vector3 value) => track.Direction switch {
            0 => value,
            1 => new Vector3(value.Z, value.Y, -value.X),
            2 => new Vector3(-value.X, value.Y, -value.Z),
            3 => new Vector3(-value.Z, value.Y, value.X),
            _ => throw new InvalidDataException($"OpenRCT2 returned invalid tower direction {track.Direction}.")
        };
    }
}
