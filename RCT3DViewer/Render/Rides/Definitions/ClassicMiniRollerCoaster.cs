using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal class ClassicMiniRollerCoaster : RideDefinition
{
    public ClassicMiniRollerCoaster() : base(RCTRideType.ClassicMiniRollerCoaster)
    {
        SupportStyle = RideSupportStyle.MetalFork;
        HasSupportPlacement = true;
        TrackHeightOffset = 0.25f;
    }

    protected override TrackRailProfile CreateProfile()
    {
        return new TrackRailProfile([
            new TrackRailProfileShape(CreateCircle(0.075f, -0.42f, 0.28f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(80, 80),
            },
            new TrackRailProfileShape(CreateCircle(0.075f,  0.42f, 0.28f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(80, 80),
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

        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25, Pitch.Slope25, Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60, Pitch.Slope60, Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp25, Pitch.None, Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToUp60, Pitch.Slope25, Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToUp25, Pitch.Slope60, Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToFlat, Pitch.Slope25, Pitch.None));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25, -Pitch.Slope25, -Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60, -Pitch.Slope60, -Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown25, Pitch.None, -Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToDown60, -Pitch.Slope25, -Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToDown25, -Pitch.Slope60, -Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToFlat, -Pitch.Slope25, Pitch.None));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp60, Pitch.None, Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToFlat, Pitch.Slope60, Pitch.None));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown60, Pitch.None, -Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToFlat, -Pitch.Slope60, Pitch.None));

        registry.RegisterMany(type => new CurveTrackPieceBuilder(type),
            RCTTrackElementType.LeftQuarterTurn5Tiles,
            RCTTrackElementType.RightQuarterTurn5Tiles,
            RCTTrackElementType.SBendLeft,
            RCTTrackElementType.SBendRight,
            RCTTrackElementType.LeftQuarterTurn3Tiles,
            RCTTrackElementType.RightQuarterTurn3Tiles,
            RCTTrackElementType.LeftQuarterTurn1Tile,
            RCTTrackElementType.RightQuarterTurn1Tile);

        // Up and down variants include curved powered lift hills.
        registry.RegisterMany(type => new CurveTrackPieceBuilder(type),
            RCTTrackElementType.LeftQuarterTurn5TilesUp25,
            RCTTrackElementType.RightQuarterTurn5TilesUp25,
            RCTTrackElementType.LeftQuarterTurn5TilesDown25,
            RCTTrackElementType.RightQuarterTurn5TilesDown25,
            RCTTrackElementType.LeftQuarterTurn3TilesUp25,
            RCTTrackElementType.RightQuarterTurn3TilesUp25,
            RCTTrackElementType.LeftQuarterTurn3TilesDown25,
            RCTTrackElementType.RightQuarterTurn3TilesDown25);

        registry.RegisterMany(type => new StraightTrackPieceBuilder(type),
            RCTTrackElementType.LeftBank,
            RCTTrackElementType.RightBank);

        registry.RegisterMany(type => new BankedCurveTrackPieceBuilder(type),
            RCTTrackElementType.FlatToLeftBank,
            RCTTrackElementType.FlatToRightBank,
            RCTTrackElementType.LeftBankToFlat,
            RCTTrackElementType.RightBankToFlat);

        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.LeftBankToUp25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.RightBankToUp25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Up25ToLeftBank));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Up25ToRightBank));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.LeftBankToDown25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.RightBankToDown25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Down25ToLeftBank));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Down25ToRightBank));

        registry.RegisterMany(type => new BankedCurveTrackPieceBuilder(type),
            RCTTrackElementType.BankedLeftQuarterTurn5Tiles,
            RCTTrackElementType.BankedRightQuarterTurn5Tiles,
            RCTTrackElementType.LeftBankedQuarterTurn3Tiles,
            RCTTrackElementType.RightBankedQuarterTurn3Tiles);

        registry.RegisterMany(type => new HelixTrackPieceBuilder(type),
            RCTTrackElementType.LeftHalfBankedHelixUpSmall,
            RCTTrackElementType.RightHalfBankedHelixUpSmall,
            RCTTrackElementType.LeftHalfBankedHelixDownSmall,
            RCTTrackElementType.RightHalfBankedHelixDownSmall,
            RCTTrackElementType.LeftHalfBankedHelixUpLarge,
            RCTTrackElementType.RightHalfBankedHelixUpLarge,
            RCTTrackElementType.LeftHalfBankedHelixDownLarge,
            RCTTrackElementType.RightHalfBankedHelixDownLarge);

        registry.RegisterMany(type => new StraightTrackPieceBuilder(type),
            RCTTrackElementType.Brakes,
            RCTTrackElementType.Down25Brakes,
            RCTTrackElementType.BlockBrakes,
            RCTTrackElementType.Booster,
            RCTTrackElementType.DiagBrakes,
            RCTTrackElementType.DiagBlockBrakes,
            RCTTrackElementType.DiagBooster,
            RCTTrackElementType.OnRidePhoto);

        registry.Register(new DiagonalFlatTrackPieceBuilder());

        registry.RegisterMany(type => new DiagonalTrackPieceBuilder(type),
            RCTTrackElementType.DiagUp25,
            RCTTrackElementType.DiagUp60,
            RCTTrackElementType.DiagFlatToUp25,
            RCTTrackElementType.DiagUp25ToUp60,
            RCTTrackElementType.DiagUp60ToUp25,
            RCTTrackElementType.DiagUp25ToFlat,
            RCTTrackElementType.DiagDown25,
            RCTTrackElementType.DiagDown60,
            RCTTrackElementType.DiagFlatToDown25,
            RCTTrackElementType.DiagDown25ToDown60,
            RCTTrackElementType.DiagDown60ToDown25,
            RCTTrackElementType.DiagDown25ToFlat,
            RCTTrackElementType.DiagFlatToUp60,
            RCTTrackElementType.DiagUp60ToFlat,
            RCTTrackElementType.DiagFlatToDown60,
            RCTTrackElementType.DiagDown60ToFlat);

        registry.RegisterMany(type => new BankedCurveTrackPieceBuilder(type),
            RCTTrackElementType.LeftEighthToDiag,
            RCTTrackElementType.RightEighthToDiag,
            RCTTrackElementType.LeftEighthToOrthogonal,
            RCTTrackElementType.RightEighthToOrthogonal,
            RCTTrackElementType.LeftEighthBankToDiag,
            RCTTrackElementType.RightEighthBankToDiag,
            RCTTrackElementType.LeftEighthBankToOrthogonal,
            RCTTrackElementType.RightEighthBankToOrthogonal,
            RCTTrackElementType.LeftEighthToDiagUp25,
            RCTTrackElementType.RightEighthToDiagUp25,
            RCTTrackElementType.LeftEighthToDiagDown25,
            RCTTrackElementType.RightEighthToDiagDown25,
            RCTTrackElementType.LeftEighthToOrthogonalUp25,
            RCTTrackElementType.RightEighthToOrthogonalUp25,
            RCTTrackElementType.LeftEighthToOrthogonalDown25,
            RCTTrackElementType.RightEighthToOrthogonalDown25,
            RCTTrackElementType.LeftEighthBankToDiagUp25,
            RCTTrackElementType.RightEighthBankToDiagUp25,
            RCTTrackElementType.LeftEighthBankToDiagDown25,
            RCTTrackElementType.RightEighthBankToDiagDown25,
            RCTTrackElementType.LeftEighthBankToOrthogonalUp25,
            RCTTrackElementType.RightEighthBankToOrthogonalUp25,
            RCTTrackElementType.LeftEighthBankToOrthogonalDown25,
            RCTTrackElementType.RightEighthBankToOrthogonalDown25);

        registry.RegisterMany(type => new DiagonalTrackPieceBuilder(type),
            RCTTrackElementType.DiagFlatToLeftBank,
            RCTTrackElementType.DiagFlatToRightBank,
            RCTTrackElementType.DiagLeftBankToFlat,
            RCTTrackElementType.DiagRightBankToFlat,
            RCTTrackElementType.DiagLeftBankToUp25,
            RCTTrackElementType.DiagRightBankToUp25,
            RCTTrackElementType.DiagUp25ToLeftBank,
            RCTTrackElementType.DiagUp25ToRightBank,
            RCTTrackElementType.DiagLeftBankToDown25,
            RCTTrackElementType.DiagRightBankToDown25,
            RCTTrackElementType.DiagDown25ToLeftBank,
            RCTTrackElementType.DiagDown25ToRightBank,
            RCTTrackElementType.DiagLeftBank,
            RCTTrackElementType.DiagRightBank);
    }

    protected override void GetSupportPlacements(RCTTrackElementData track, List<TrackSupportPlacement> placements)
    {
        placements.Clear();

        if (track.TrackType is RCTTrackElementType.BeginStation or RCTTrackElementType.MiddleStation or RCTTrackElementType.EndStation) {
            if ((track.TileX & 1) == (track.TileY & 1))
                placements.Add(TrackSupportPlacement.OnTrack());
            return;
        }

        if (IsMultiTileCurve(track.TrackType)) {
            if (track.Sequence == 0 || track.Sequence == track.SequenceCount - 1)
                placements.Add(TrackSupportPlacement.OnTrack());
            return;
        }

        if (IsDiagonalTrack(track.TrackType)) {
            if (track.Sequence == 3)
                placements.Add(TrackSupportPlacement.OnTrack(heightExtra: GetSupportHeightExtra(track.TrackType)));
            return;
        }

        if ((track.TileX & 1) == (track.TileY & 1))
            placements.Add(TrackSupportPlacement.OnTrack(heightExtra: GetSupportHeightExtra(track.TrackType)));
    }

    private static bool IsDiagonalTrack(RCTTrackElementType trackType) => trackType is
        RCTTrackElementType.DiagFlat
        or RCTTrackElementType.DiagUp25 or RCTTrackElementType.DiagDown25
        or RCTTrackElementType.DiagFlatToUp25 or RCTTrackElementType.DiagUp25ToFlat
        or RCTTrackElementType.DiagFlatToDown25 or RCTTrackElementType.DiagDown25ToFlat
        or RCTTrackElementType.DiagFlatToLeftBank or RCTTrackElementType.DiagFlatToRightBank
        or RCTTrackElementType.DiagLeftBankToFlat or RCTTrackElementType.DiagRightBankToFlat
        or RCTTrackElementType.DiagLeftBankToUp25 or RCTTrackElementType.DiagRightBankToUp25
        or RCTTrackElementType.DiagUp25ToLeftBank or RCTTrackElementType.DiagUp25ToRightBank
        or RCTTrackElementType.DiagLeftBankToDown25 or RCTTrackElementType.DiagRightBankToDown25
        or RCTTrackElementType.DiagDown25ToLeftBank or RCTTrackElementType.DiagDown25ToRightBank
        or RCTTrackElementType.DiagLeftBank or RCTTrackElementType.DiagRightBank;

    private static bool IsMultiTileCurve(RCTTrackElementType trackType) => trackType is
        RCTTrackElementType.LeftQuarterTurn5Tiles or RCTTrackElementType.RightQuarterTurn5Tiles
        or RCTTrackElementType.BankedLeftQuarterTurn5Tiles or RCTTrackElementType.BankedRightQuarterTurn5Tiles
        or RCTTrackElementType.LeftQuarterTurn5TilesUp25 or RCTTrackElementType.RightQuarterTurn5TilesUp25
        or RCTTrackElementType.LeftQuarterTurn5TilesDown25 or RCTTrackElementType.RightQuarterTurn5TilesDown25
        or RCTTrackElementType.SBendLeft or RCTTrackElementType.SBendRight
        or RCTTrackElementType.LeftEighthToDiag or RCTTrackElementType.RightEighthToDiag
        or RCTTrackElementType.LeftEighthToOrthogonal or RCTTrackElementType.RightEighthToOrthogonal
        or RCTTrackElementType.LeftEighthBankToDiag or RCTTrackElementType.RightEighthBankToDiag
        or RCTTrackElementType.LeftEighthBankToOrthogonal or RCTTrackElementType.RightEighthBankToOrthogonal
        or RCTTrackElementType.LeftEighthToDiagUp25 or RCTTrackElementType.RightEighthToDiagUp25
        or RCTTrackElementType.LeftEighthToDiagDown25 or RCTTrackElementType.RightEighthToDiagDown25
        or RCTTrackElementType.LeftEighthToOrthogonalUp25 or RCTTrackElementType.RightEighthToOrthogonalUp25
        or RCTTrackElementType.LeftEighthToOrthogonalDown25 or RCTTrackElementType.RightEighthToOrthogonalDown25
        or RCTTrackElementType.LeftEighthBankToDiagUp25 or RCTTrackElementType.RightEighthBankToDiagUp25
        or RCTTrackElementType.LeftEighthBankToDiagDown25 or RCTTrackElementType.RightEighthBankToDiagDown25
        or RCTTrackElementType.LeftEighthBankToOrthogonalUp25 or RCTTrackElementType.RightEighthBankToOrthogonalUp25
        or RCTTrackElementType.LeftEighthBankToOrthogonalDown25 or RCTTrackElementType.RightEighthBankToOrthogonalDown25
        or RCTTrackElementType.LeftQuarterTurn3Tiles or RCTTrackElementType.RightQuarterTurn3Tiles
        or RCTTrackElementType.LeftBankedQuarterTurn3Tiles or RCTTrackElementType.RightBankedQuarterTurn3Tiles
        or RCTTrackElementType.LeftQuarterTurn3TilesUp25 or RCTTrackElementType.RightQuarterTurn3TilesUp25
        or RCTTrackElementType.LeftQuarterTurn3TilesDown25 or RCTTrackElementType.RightQuarterTurn3TilesDown25
        or RCTTrackElementType.LeftHalfBankedHelixUpSmall or RCTTrackElementType.RightHalfBankedHelixUpSmall
        or RCTTrackElementType.LeftHalfBankedHelixDownSmall or RCTTrackElementType.RightHalfBankedHelixDownSmall
        or RCTTrackElementType.LeftHalfBankedHelixUpLarge or RCTTrackElementType.RightHalfBankedHelixUpLarge
        or RCTTrackElementType.LeftHalfBankedHelixDownLarge or RCTTrackElementType.RightHalfBankedHelixDownLarge;

    private static int GetSupportHeightExtra(RCTTrackElementType trackType) => trackType switch {
        RCTTrackElementType.Up25 or RCTTrackElementType.Down25
            or RCTTrackElementType.DiagUp25 or RCTTrackElementType.DiagDown25 => 8,
        RCTTrackElementType.Up60 or RCTTrackElementType.Down60
            or RCTTrackElementType.DiagUp60 or RCTTrackElementType.DiagDown60 => 16,
        RCTTrackElementType.FlatToUp25 or RCTTrackElementType.FlatToDown25
            or RCTTrackElementType.DiagFlatToUp25 or RCTTrackElementType.DiagFlatToDown25 => 3,
        RCTTrackElementType.Up25ToFlat or RCTTrackElementType.Down25ToFlat
            or RCTTrackElementType.DiagUp25ToFlat or RCTTrackElementType.DiagDown25ToFlat => 6,
        RCTTrackElementType.FlatToUp60 or RCTTrackElementType.FlatToDown60
            or RCTTrackElementType.DiagFlatToUp60 or RCTTrackElementType.DiagFlatToDown60 => 5,
        RCTTrackElementType.Up60ToFlat or RCTTrackElementType.Down60ToFlat
            or RCTTrackElementType.DiagUp60ToFlat or RCTTrackElementType.DiagDown60ToFlat => 12,
        _ => 0,
    };
}
