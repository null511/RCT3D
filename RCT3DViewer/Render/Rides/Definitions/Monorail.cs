using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class Monorail : RideDefinition
{
    public Monorail() : base(RCTRideType.Monorail)
    {
        SupportStyle = RideSupportStyle.MetalBoxed;
        HasSupportPlacement = true;
        TrackHeightOffset = 0.2f;
    }

    protected override TrackRailProfile CreateProfile()
    {
        return new TrackRailProfile([
            new TrackRailProfileShape([
                new Vector2(0.20f, 0.40f),
                new Vector2(0.20f, 0.50f),
                new Vector2(0.20f, 0.55f),
                new Vector2(0.15f, 0.60f),
                new Vector2(0.10f, 0.60f),
                new Vector2(-0.10f, 0.60f),
                new Vector2(-0.15f, 0.60f),
                new Vector2(-0.20f, 0.55f),
                new Vector2(-0.20f, 0.50f),
                new Vector2(-0.20f, 0.40f),
            ], false) {
                Swatch = TrackRailSwatch.Main,
                Specular = new TrackRailSpecularData(180, 0),
            },
            new TrackRailProfileShape([
                new Vector2(-0.20f, 0.40f),
                new Vector2(-0.20f, 0.09f),
                new Vector2(-0.20f, 0.07f),
                new Vector2(-0.18f, 0.05f),
                new Vector2(-0.16f, 0.05f),
                new Vector2(0.16f, 0.05f),
                new Vector2(0.18f, 0.05f),
                new Vector2(0.20f, 0.07f),
                new Vector2(0.20f, 0.09f),
                new Vector2(0.20f, 0.40f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(240, 0),
            },
        ]);
    }

    protected override void BuildRegistry(TrackPieceBuilderRegistry registry)
    {
        registry.RegisterMany(type => new StraightTrackPieceBuilder(type),
            RCTTrackElementType.Flat,
            RCTTrackElementType.EndStation,
            RCTTrackElementType.BeginStation,
            RCTTrackElementType.MiddleStation);

        registry.Register(new DiagonalFlatTrackPieceBuilder());

        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25, false, false));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp25, true, false));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToFlat, false, true));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25, false, false));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown25, true, false));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToFlat, false, true));

        registry.RegisterMany(type => new CurveTrackPieceBuilder(type),
            RCTTrackElementType.LeftQuarterTurn5Tiles,
            RCTTrackElementType.RightQuarterTurn5Tiles,
            RCTTrackElementType.SBendLeft,
            RCTTrackElementType.SBendRight,
            RCTTrackElementType.LeftQuarterTurn3Tiles,
            RCTTrackElementType.RightQuarterTurn3Tiles,
            RCTTrackElementType.LeftQuarterTurn1Tile,
            RCTTrackElementType.RightQuarterTurn1Tile,
            RCTTrackElementType.LeftEighthToDiag,
            RCTTrackElementType.RightEighthToDiag,
            RCTTrackElementType.LeftEighthToOrthogonal,
            RCTTrackElementType.RightEighthToOrthogonal,
            RCTTrackElementType.LeftEighthBankToDiag,
            RCTTrackElementType.RightEighthBankToDiag,
            RCTTrackElementType.LeftEighthBankToOrthogonal,
            RCTTrackElementType.RightEighthBankToOrthogonal,
            RCTTrackElementType.LeftQuarterTurn5TilesCovered,
            RCTTrackElementType.RightQuarterTurn5TilesCovered,
            RCTTrackElementType.SBendLeftCovered,
            RCTTrackElementType.SBendRightCovered,
            RCTTrackElementType.LeftQuarterTurn3TilesCovered,
            RCTTrackElementType.RightQuarterTurn3TilesCovered);
    }

    protected override void GetSupportPlacements(RCTTrackElementData track, List<TrackSupportPlacement> placements)
    {
        placements.Clear();

        if (track.TrackType is RCTTrackElementType.BeginStation or RCTTrackElementType.MiddleStation or RCTTrackElementType.EndStation) {
            placements.Add(TrackSupportPlacement.OnTrack(0.31f));
            placements.Add(TrackSupportPlacement.OnTrack(-0.31f));

            return;
        }

        if (track.TrackType is RCTTrackElementType.LeftQuarterTurn5Tiles or RCTTrackElementType.RightQuarterTurn5Tiles
            or RCTTrackElementType.LeftQuarterTurn5TilesCovered or RCTTrackElementType.RightQuarterTurn5TilesCovered) {
            if (track.Sequence is 0 or 6)
                placements.Add(TrackSupportPlacement.OnTrack());

            return;
        }

        if (track.TrackType is RCTTrackElementType.LeftQuarterTurn3Tiles or RCTTrackElementType.RightQuarterTurn3Tiles
            or RCTTrackElementType.LeftQuarterTurn3TilesCovered or RCTTrackElementType.RightQuarterTurn3TilesCovered) {
            if (track.Sequence is 0 or 3)
                placements.Add(TrackSupportPlacement.OnTrack());

            return;
        }

        if (track.TrackType is RCTTrackElementType.SBendLeft or RCTTrackElementType.SBendRight
            or RCTTrackElementType.SBendLeftCovered or RCTTrackElementType.SBendRightCovered) {
            var trackProgress = (track.Sequence + 0.5f) / track.SequenceCount;

            if (track.Sequence is 0 or 3) {
                placements.Add(TrackSupportPlacement.OnTrack(trackProgress: trackProgress));
                return;
            }

            if (track.Sequence is 1 or 2) {
                placements.Add(TrackSupportPlacement.OnTrack(trackProgress: trackProgress));
            }

            return;
        }

        if (track.TrackType.IsEighthTurn()) {
            const float supportInsetProgress = 16.0f / 87.0f;

            if (track.Sequence == 0)
                placements.Add(TrackSupportPlacement.OnTrack(trackProgress: supportInsetProgress));
            else if (track.Sequence == track.SequenceCount - 1)
                placements.Add(TrackSupportPlacement.OnTrack(trackProgress: 1.0f - supportInsetProgress));

            return;
        }

        if (track.TrackType.IsDiagonal()) {
            if (track.Sequence == 3)
                placements.Add(TrackSupportPlacement.OnTrack(heightExtra: GetSlopeSupportHeightExtra(track.TrackType)));

            return;
        }

        if ((track.TileX & 1) == (track.TileY & 1))
            placements.Add(TrackSupportPlacement.OnTrack(heightExtra: GetSlopeSupportHeightExtra(track.TrackType)));
    }

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
