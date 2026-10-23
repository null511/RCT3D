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
                new Vector2(-0.20f, 0.40f),
                new Vector2(-0.20f, 0.50f),
                new Vector2(-0.20f, 0.55f),
                new Vector2(-0.15f, 0.60f),
                new Vector2(-0.10f, 0.60f),
                new Vector2(0.10f, 0.60f),
                new Vector2(0.15f, 0.60f),
                new Vector2(0.20f, 0.55f),
                new Vector2(0.20f, 0.50f),
                new Vector2(0.20f, 0.40f),
            ], false) {
                Swatch = TrackRailSwatch.Main,
                Specular = new TrackRailSpecularData(200, 0),
            },
            new TrackRailProfileShape([
                new Vector2(0.20f, 0.40f),
                new Vector2(0.20f, 0.09f),
                new Vector2(0.20f, 0.07f),
                new Vector2(0.18f, 0.05f),
                new Vector2(0.16f, 0.05f),
                new Vector2(-0.16f, 0.05f),
                new Vector2(-0.18f, 0.05f),
                new Vector2(-0.20f, 0.07f),
                new Vector2(-0.20f, 0.09f),
                new Vector2(-0.20f, 0.40f),
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

        registry.RegisterMany(type => new StraightTrackPieceBuilder(type, 0.0f),
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

        return registry;
    }
}
