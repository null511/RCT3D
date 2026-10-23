using RCT3D.Interop;
using RCT3D.Render.Tracks.Meshing;
using System.Numerics;

namespace RCT3D.Render.Tracks.Rides;

internal static class ClassicMiniRollerCoaster
{
    public static TrackRideConfiguration Build()
    {
        var railProfile = new TrackRailProfile([
            CreateCircularRail(-0.42f),
            CreateCircularRail( 0.42f),
        ]);

        return new TrackRideConfiguration(
            RCTRideType.ClassicMiniRollerCoaster,
            CreateRegistry(),
            [new SweptRailProfileGenerator(railProfile)]);
    }

    private static TrackRailProfileShape CreateCircularRail(float centerX)
    {
        const float radius = 0.075f;
        const float centerY = 0.28f;

        return new TrackRailProfileShape([
            new Vector2(centerX, centerY + radius),
            new Vector2(centerX + radius * 0.95106f, centerY + radius * 0.30902f),
            new Vector2(centerX + radius * 0.58779f, centerY - radius * 0.80902f),
            new Vector2(centerX - radius * 0.58779f, centerY - radius * 0.80902f),
            new Vector2(centerX - radius * 0.95106f, centerY + radius * 0.30902f),
        ], true) {
            Swatch = TrackRailSwatch.Main,
            Specular = new TrackRailSpecularData(100, 100),
        };
    }

    private static TrackPieceBuilderRegistry CreateRegistry()
    {
        var registry = new TrackPieceBuilderRegistry();

        registry.RegisterMany(type => new StraightTrackPieceBuilder(type, 0.0f),
            RCTTrackElementType.Flat,
            RCTTrackElementType.EndStation,
            RCTTrackElementType.BeginStation,
            RCTTrackElementType.MiddleStation);

        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25, TrackCenterlineGeometry.Pitch25Slope, TrackCenterlineGeometry.Pitch25Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60, TrackCenterlineGeometry.Pitch60Slope, TrackCenterlineGeometry.Pitch60Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp25, 0.0f, TrackCenterlineGeometry.Pitch25Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToUp60, TrackCenterlineGeometry.Pitch25Slope, TrackCenterlineGeometry.Pitch60Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToUp25, TrackCenterlineGeometry.Pitch60Slope, TrackCenterlineGeometry.Pitch25Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToFlat, TrackCenterlineGeometry.Pitch25Slope, 0.0f));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25, -TrackCenterlineGeometry.Pitch25Slope, -TrackCenterlineGeometry.Pitch25Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60, -TrackCenterlineGeometry.Pitch60Slope, -TrackCenterlineGeometry.Pitch60Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown25, 0.0f, -TrackCenterlineGeometry.Pitch25Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToDown60, -TrackCenterlineGeometry.Pitch25Slope, -TrackCenterlineGeometry.Pitch60Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToDown25, -TrackCenterlineGeometry.Pitch60Slope, -TrackCenterlineGeometry.Pitch25Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToFlat, -TrackCenterlineGeometry.Pitch25Slope, 0.0f));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp60, 0.0f, TrackCenterlineGeometry.Pitch60Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToFlat, TrackCenterlineGeometry.Pitch60Slope, 0.0f));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown60, 0.0f, -TrackCenterlineGeometry.Pitch60Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToFlat, -TrackCenterlineGeometry.Pitch60Slope, 0.0f));

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

        registry.RegisterMany(type => new StraightTrackPieceBuilder(type, 0.0f),
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

        registry.RegisterMany(type => new StraightTrackPieceBuilder(type, 0.0f),
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

        return registry;
    }
}
