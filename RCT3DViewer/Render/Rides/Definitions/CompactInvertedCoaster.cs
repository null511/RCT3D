using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class CompactInvertedCoaster : RideDefinition
{
    public CompactInvertedCoaster() : base(RCTRideType.CompactInvertedCoaster)
    {
        SupportStyle = RideSupportStyle.MetalTubesInverted;
        CenterlineHeightOffset = 3.0f;
    }

    protected override TrackRailProfile CreateProfile()
    {
        return new TrackRailProfile([
            new TrackRailProfileShape(CreateCircle(0.06f, -0.42f, 1.12f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(100, 0),
            },
            new TrackRailProfileShape(CreateCircle(0.18f,  0.00f, 1.20f, 6), true) {
                Swatch = TrackRailSwatch.Main,
                Specular = new TrackRailSpecularData(100, 0),
            },
            new TrackRailProfileShape(CreateCircle(0.06f,  0.42f, 1.12f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(100, 0),
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

        registry.Register(new LongBaseSlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp60LongBase, Pitch.None, Pitch.Slope60));

        registry.RegisterMany(type => new CurveTrackPieceBuilder(type),
            RCTTrackElementType.LeftQuarterTurn5Tiles,
            RCTTrackElementType.RightQuarterTurn5Tiles,
            RCTTrackElementType.SBendLeft,
            RCTTrackElementType.SBendRight,
            RCTTrackElementType.LeftQuarterTurn3Tiles,
            RCTTrackElementType.RightQuarterTurn3Tiles,
            RCTTrackElementType.LeftQuarterTurn1Tile,
            RCTTrackElementType.RightQuarterTurn1Tile);

        registry.RegisterMany(type => new CurveTrackPieceBuilder(type),
            RCTTrackElementType.LeftQuarterTurn5TilesUp25,
            RCTTrackElementType.RightQuarterTurn5TilesUp25,
            RCTTrackElementType.LeftQuarterTurn5TilesDown25,
            RCTTrackElementType.RightQuarterTurn5TilesDown25,
            RCTTrackElementType.LeftQuarterTurn3TilesUp25,
            RCTTrackElementType.RightQuarterTurn3TilesUp25,
            RCTTrackElementType.LeftQuarterTurn3TilesDown25,
            RCTTrackElementType.RightQuarterTurn3TilesDown25);

        registry.RegisterMany(type => new SteepQuarterTurnTrackPieceBuilder(type),
            RCTTrackElementType.LeftQuarterTurn1TileUp60,
            RCTTrackElementType.RightQuarterTurn1TileUp60,
            RCTTrackElementType.LeftQuarterTurn1TileDown60,
            RCTTrackElementType.RightQuarterTurn1TileDown60);

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

        registry.RegisterMany(type => new StraightTrackPieceBuilder(type),
            RCTTrackElementType.Brakes,
            RCTTrackElementType.Down25Brakes,
            RCTTrackElementType.BlockBrakes,
            RCTTrackElementType.DiagBrakes,
            RCTTrackElementType.DiagBlockBrakes,
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

        registry.RegisterMany(type => new VerticalLoopTrackPieceBuilder(type),
            RCTTrackElementType.LeftVerticalLoop,
            RCTTrackElementType.RightVerticalLoop);

        registry.RegisterMany(type => new TwistTrackPieceBuilder(type),
            RCTTrackElementType.LeftTwistDownToUp,
            RCTTrackElementType.RightTwistDownToUp,
            RCTTrackElementType.RightTwistUpToDown);

        registry.Register(new BankedCurveTrackPieceBuilder(RCTTrackElementType.RightQuarterBankedHelixLargeUp));
    }
}
