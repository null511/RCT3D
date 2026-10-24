using RCT3D.Interop;
using System.Numerics;

namespace RCT3D.Render.Tracks.Meshing;

internal readonly record struct TrackSupportPlacement(float TileOffsetX, float TileOffsetZ, int HeightExtra = 0);

internal interface ITrackSupportPlacementMap
{
    void GetPlacements(RCTTrackElementData track, List<TrackSupportPlacement> placements);
}

internal sealed class MonorailTrackSupportPlacementMap : ITrackSupportPlacementMap
{
    private const float StationPostOffset = 0.31f;

    public void GetPlacements(RCTTrackElementData track, List<TrackSupportPlacement> placements)
    {
        placements.Clear();

        if (track.TrackType is RCTTrackElementType.BeginStation or RCTTrackElementType.MiddleStation or RCTTrackElementType.EndStation) {
            var trackDirection = TrackCenterlineGeometry.GetTrackDirection(track.Direction);
            var lateralDirection = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, trackDirection));
            placements.Add(new TrackSupportPlacement(
                0.5f + lateralDirection.X * StationPostOffset / WorldUnits.TileSize,
                0.5f + lateralDirection.Z * StationPostOffset / WorldUnits.TileSize));
            placements.Add(new TrackSupportPlacement(
                0.5f - lateralDirection.X * StationPostOffset / WorldUnits.TileSize,
                0.5f - lateralDirection.Z * StationPostOffset / WorldUnits.TileSize));
            return;
        }

        if (track.TrackType is RCTTrackElementType.LeftQuarterTurn5Tiles or RCTTrackElementType.RightQuarterTurn5Tiles
            or RCTTrackElementType.LeftQuarterTurn5TilesCovered or RCTTrackElementType.RightQuarterTurn5TilesCovered) {
            if (track.Sequence is 0 or 6)
                placements.Add(new TrackSupportPlacement(0.5f, 0.5f));
            return;
        }

        if (track.TrackType is RCTTrackElementType.LeftQuarterTurn3Tiles or RCTTrackElementType.RightQuarterTurn3Tiles
            or RCTTrackElementType.LeftQuarterTurn3TilesCovered or RCTTrackElementType.RightQuarterTurn3TilesCovered) {
            if (track.Sequence is 0 or 3)
                placements.Add(new TrackSupportPlacement(0.5f, 0.5f));
            return;
        }

        if (track.TrackType is RCTTrackElementType.SBendLeft or RCTTrackElementType.SBendRight
            or RCTTrackElementType.SBendLeftCovered or RCTTrackElementType.SBendRightCovered) {
            if (track.Sequence is 0 or 3) {
                placements.Add(new TrackSupportPlacement(0.5f, 0.5f));
                return;
            }

            if (track.Sequence is 1 or 2) {
                var trackDirection = TrackCenterlineGeometry.GetTrackDirection(track.Direction);
                var lateralDirection = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, trackDirection));
                var side = track.Sequence == 1 ? 1.0f : -1.0f;
                var offset = lateralDirection * (side * 0.25f);
                placements.Add(new TrackSupportPlacement(0.5f + offset.X / WorldUnits.TileSize, 0.5f + offset.Z / WorldUnits.TileSize));
            }

            return;
        }

        if (IsEighthTurn(track.TrackType)) {
            if (track.Sequence == 0)
                placements.Add(new TrackSupportPlacement(0.5f, 0.5f));
            else if (track.Sequence == 4)
                placements.Add(new TrackSupportPlacement(
                    track.Direction is 0 or 2 ? 0.78f : 0.22f,
                    track.Direction is 1 or 3 ? 0.78f : 0.22f));
            return;
        }

        if (IsDiagonal(track.TrackType)) {
            if (track.Sequence == 3)
                placements.Add(new TrackSupportPlacement(0.5f, 0.5f, GetSlopeSupportHeightExtra(track.TrackType)));
            return;
        }

        if ((track.TileX & 1) == (track.TileY & 1))
            placements.Add(new TrackSupportPlacement(0.5f, 0.5f, GetSlopeSupportHeightExtra(track.TrackType)));
    }

    private static bool IsEighthTurn(RCTTrackElementType trackType) => trackType is
        RCTTrackElementType.LeftEighthToDiag or RCTTrackElementType.RightEighthToDiag
        or RCTTrackElementType.LeftEighthToOrthogonal or RCTTrackElementType.RightEighthToOrthogonal
        or RCTTrackElementType.LeftEighthBankToDiag or RCTTrackElementType.RightEighthBankToDiag
        or RCTTrackElementType.LeftEighthBankToOrthogonal or RCTTrackElementType.RightEighthBankToOrthogonal;

    private static bool IsDiagonal(RCTTrackElementType trackType) => trackType is
        RCTTrackElementType.DiagFlat or RCTTrackElementType.DiagUp25 or RCTTrackElementType.DiagDown25
        or RCTTrackElementType.DiagFlatToUp25 or RCTTrackElementType.DiagUp25ToFlat
        or RCTTrackElementType.DiagFlatToDown25 or RCTTrackElementType.DiagDown25ToFlat;

    private static int GetSlopeSupportHeightExtra(RCTTrackElementType trackType) => trackType switch {
        RCTTrackElementType.Up25 or RCTTrackElementType.Down25 => 8,
        RCTTrackElementType.FlatToUp25 or RCTTrackElementType.FlatToDown25 => 3,
        RCTTrackElementType.Up25ToFlat or RCTTrackElementType.Down25ToFlat => 6,
        RCTTrackElementType.DiagUp25 or RCTTrackElementType.DiagDown25 => 8,
        RCTTrackElementType.DiagFlatToUp25 or RCTTrackElementType.DiagFlatToDown25 => 0,
        RCTTrackElementType.DiagUp25ToFlat or RCTTrackElementType.DiagDown25ToFlat => 4,
        _ => 0,
    };
}
