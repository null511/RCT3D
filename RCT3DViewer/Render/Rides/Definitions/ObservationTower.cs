using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class ObservationTower : RideDefinition
{
    public ObservationTower() : base(RCTRideType.ObservationTower)
    {
        PlatformSurface = RidePlatformSurfaceType.Metal;
        PlatformRailing = RidePlatformRailingType.Fenced;

        DirectMeshBuilder = new ObservationTowerMeshBuilder();
    }
}

internal sealed class ObservationTowerMeshBuilder : IDirectTrackMeshBuilder
{
    private const int SectionBaseHeight = 4;
    private const int BaseSectionCount = 3;
    private const float CapHeight = 8f * WorldUnits.NativeUnit;

    private const float ColumnRadius = 4f * WorldUnits.NativeUnit;
    private const float SectionHeight = SectionBaseHeight * WorldUnits.HeightPerBaseUnit;

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
        var altColor = TrackMeshPartGenerator.GetPaletteColor(track.AdditionalPaletteIndex, palette);

        for (var section = 0; section < sectionCount; section++) {
            var bottom = section * SectionHeight;
            var top = bottom + SectionHeight;
            var capped = terminal && section == sectionCount - 1;
            var frameTop = capped ? top - CapHeight : top;

            AddColumn(bottom, frameTop, 12, capped, mainColor);

            // TODO: add 4 alt-colored spines

            if (!capped) continue;

            // TODO: ?
        }

        return;

        void AddColumn(float bottom, float top, int numSides, bool closeTop, TrackRgba8 color) {
            for (var i = 0; i < numSides; i++) {
                var angle = i * MathF.Tau / numSides;
                var nextAngle = (i + 1) * MathF.Tau / numSides;
                var start = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
                var end = new Vector3(MathF.Cos(nextAngle), 0, MathF.Sin(nextAngle));
                var a = start * ColumnRadius + Vector3.UnitY * bottom;
                var b = start * ColumnRadius + Vector3.UnitY * top;
                var c = end * ColumnRadius + Vector3.UnitY * top;
                var d = end * ColumnRadius + Vector3.UnitY * bottom;
                AddQuad(a, b, c, d, Vector3.Normalize(start + end), color);
                if (!closeTop) continue;

                var centerIndex = AddVertex(Vector3.UnitY * top, Vector3.UnitY, color);
                var startIndex = AddVertex(b, Vector3.UnitY, color);
                var endIndex = AddVertex(c, Vector3.UnitY, color);
                geometry.AddIndexedTriangle(centerIndex, startIndex, endIndex);
            }
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
