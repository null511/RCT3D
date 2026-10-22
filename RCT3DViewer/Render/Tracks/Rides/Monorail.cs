using RCT3D.Interop;
using RCT3D.Render.Tracks.Meshing;
using System.Numerics;

namespace RCT3D.Render.Tracks.Rides;

internal class Monorail
{
    public static TrackRideConfiguration Build()
    {
        TrackRailProfile railProfile = new([
            new TrackRailProfileShape([
                new Vector2(-0.20f, 0.05f),
                new Vector2(-0.20f, 0.15f),
                new Vector2(-0.20f, 0.20f),
                new Vector2(-0.15f, 0.25f),
                new Vector2(-0.10f, 0.25f),
                new Vector2(0.10f, 0.25f),
                new Vector2(0.15f, 0.25f),
                new Vector2(0.20f, 0.20f),
                new Vector2(0.20f, 0.15f),
                new Vector2(0.20f, 0.05f),
            ], false) {
                Swatch = TrackRailSwatch.Main,
                Specular = new TrackRailSpecularData(200, 0),
            },
            new TrackRailProfileShape([
                new Vector2(0.20f, 0.05f),
                new Vector2(0.20f, -0.26f),
                new Vector2(0.20f, -0.28f),
                new Vector2(0.18f, -0.30f),
                new Vector2(0.16f, -0.30f),
                new Vector2(-0.16f, -0.30f),
                new Vector2(-0.18f, -0.30f),
                new Vector2(-0.20f, -0.28f),
                new Vector2(-0.20f, -0.26f),
                new Vector2(-0.20f, 0.05f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(120, 255),
            },
        ]);

        return new TrackRideConfiguration(RCTRideType.Monorail, CreateRegistry(), [new SweptRailProfileGenerator(railProfile)]);
    }

    public static TrackPieceBuilderRegistry CreateRegistry()
    {
        var registry = new TrackPieceBuilderRegistry();

        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.Flat, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.EndStation, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.BeginStation, 0.0f));
        registry.Register(new StraightTrackPieceBuilder(RCTTrackElementType.MiddleStation, 0.0f));
        registry.Register(new DiagonalFlatTrackPieceBuilder());

        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25, false, false));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp25, true, false));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToFlat, false, true));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25, false, false));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown25, true, false));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToFlat, false, true));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn5Tiles));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn5Tiles));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.SBendLeft));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.SBendRight));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn3Tiles));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn3Tiles));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn1Tile));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn1Tile));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftEighthToDiag));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightEighthToDiag));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftEighthToOrthogonal));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightEighthToOrthogonal));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftEighthBankToDiag));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightEighthBankToDiag));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftEighthBankToOrthogonal));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightEighthBankToOrthogonal));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn5TilesCovered));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn5TilesCovered));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.SBendLeftCovered));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.SBendRightCovered));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.LeftQuarterTurn3TilesCovered));
        registry.Register(new CurveTrackPieceBuilder(RCTTrackElementType.RightQuarterTurn3TilesCovered));

        return registry;
    }
}
