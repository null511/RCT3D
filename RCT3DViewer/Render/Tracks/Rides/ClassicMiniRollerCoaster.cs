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
        const float centerY = 0.08f;

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

        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.Flat, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.EndStation, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.BeginStation, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.MiddleStation, 0.0f));

        const float pitch25 = 0.4663077f;
        const float pitch60 = 1.7320508f;
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25, pitch25, pitch25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60, pitch60, pitch60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp25, 0.0f, pitch25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToUp60, pitch25, pitch60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToUp25, pitch60, pitch25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToFlat, pitch25, 0.0f));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25, -pitch25, -pitch25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60, -pitch60, -pitch60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown25, 0.0f, -pitch25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToDown60, -pitch25, -pitch60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToDown25, -pitch60, -pitch25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToFlat, -pitch25, 0.0f));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp60, 0.0f, pitch60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToFlat, pitch60, 0.0f));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown60, 0.0f, -pitch60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToFlat, -pitch60, 0.0f));

        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn5Tiles));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn5Tiles));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.SBendLeft));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.SBendRight));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn3Tiles));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn3Tiles));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn1Tile));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn1Tile));

        // up variants include curved powered lift hills
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn5TilesUp25));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn5TilesUp25));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn5TilesDown25));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn5TilesDown25));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn3TilesUp25));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn3TilesUp25));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn3TilesDown25));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn3TilesDown25));

        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.LeftBank, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.RightBank, 0.0f));

        registry.Register(new BankedCurveTrackPieceBuilder(RCTTrackElementType.FlatToLeftBank));
        registry.Register(new BankedCurveTrackPieceBuilder(RCTTrackElementType.FlatToRightBank));
        registry.Register(new BankedCurveTrackPieceBuilder(RCTTrackElementType.LeftBankToFlat));
        registry.Register(new BankedCurveTrackPieceBuilder(RCTTrackElementType.RightBankToFlat));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.LeftBankToUp25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.RightBankToUp25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Up25ToLeftBank));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Up25ToRightBank));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.LeftBankToDown25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.RightBankToDown25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Down25ToLeftBank));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Down25ToRightBank));
        registry.Register(new BankedCurveTrackPieceBuilder(RCTTrackElementType.BankedLeftQuarterTurn5Tiles));
        registry.Register(new BankedCurveTrackPieceBuilder(RCTTrackElementType.BankedRightQuarterTurn5Tiles));
        registry.Register(new BankedCurveTrackPieceBuilder(RCTTrackElementType.LeftBankedQuarterTurn3Tiles));
        registry.Register(new BankedCurveTrackPieceBuilder(RCTTrackElementType.RightBankedQuarterTurn3Tiles));

        // Temporarily disabled to verify which builder handles curved lift hill track in the viewer.
        // registry.Register(new CurvedLiftHillTrackPieceBuilder(RCTTrackElementType.LeftCurvedLiftHill));
        // registry.Register(new CurvedLiftHillTrackPieceBuilder(RCTTrackElementType.RightCurvedLiftHill));

        registry.Register(new HelixTrackPieceBuilder(RCTTrackElementType.LeftHalfBankedHelixUpSmall));
        registry.Register(new HelixTrackPieceBuilder(RCTTrackElementType.RightHalfBankedHelixUpSmall));
        registry.Register(new HelixTrackPieceBuilder(RCTTrackElementType.LeftHalfBankedHelixDownSmall));
        registry.Register(new HelixTrackPieceBuilder(RCTTrackElementType.RightHalfBankedHelixDownSmall));
        registry.Register(new HelixTrackPieceBuilder(RCTTrackElementType.LeftHalfBankedHelixUpLarge));
        registry.Register(new HelixTrackPieceBuilder(RCTTrackElementType.RightHalfBankedHelixUpLarge));
        registry.Register(new HelixTrackPieceBuilder(RCTTrackElementType.LeftHalfBankedHelixDownLarge));
        registry.Register(new HelixTrackPieceBuilder(RCTTrackElementType.RightHalfBankedHelixDownLarge));

        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.Brakes, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.Down25Brakes, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.BlockBrakes, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.Booster, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.DiagBrakes, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.DiagBlockBrakes, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.DiagBooster, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.OnRidePhoto, 0.0f));

        registry.Register(new DiagonalFlatTrackPieceBuilder());
        foreach (var pieceType in new[] {
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
            RCTTrackElementType.DiagDown60ToFlat,
        }) {
            registry.Register(new DiagonalTrackPieceBuilder(pieceType));
        }
        foreach (var pieceType in new[] {
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
            RCTTrackElementType.RightEighthBankToOrthogonalDown25,
        }) {
            registry.Register(new BankedCurveTrackPieceBuilder(pieceType));
        }
        foreach (var pieceType in new[] {
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
            RCTTrackElementType.DiagRightBank,
        }) {
            registry.Register(new DiagonalTrackPieceBuilder(pieceType));
        }

        return registry;
    }
}
