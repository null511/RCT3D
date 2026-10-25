using RCT3D.Interop;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class MineTrainCoaster : RideDefinition
{
    public MineTrainCoaster() : base(RCTRideType.MineTrainCoaster)
    {
        CenterlineHeightOffset = 0.2f;
    }

    protected override TrackRailProfile CreateProfile()
    {
        const float RailSeparation = 0.48f;

        return new TrackRailProfile([
            .. CreateRail(-RailSeparation*0.5f),
            .. CreateRail( RailSeparation*0.5f),
        ]);
    }

    private static IEnumerable<TrackRailProfileShape> CreateRail(float centerX)
    {
        const float halfWidth = 0.02f;
        const float halfHeight = 0.03f;

        // top
        yield return CreateRailFace([
            new Vector2(centerX + halfWidth, halfHeight),
            new Vector2(centerX - halfWidth, halfHeight),
        ], 40);

        // left
        yield return CreateRailFace([
            new Vector2(centerX - halfWidth,  halfHeight),
            new Vector2(centerX - halfWidth, -halfHeight),
        ], 90);

        // bottom
        yield return CreateRailFace([
            new Vector2(centerX - halfWidth, -halfHeight),
            new Vector2(centerX + halfWidth, -halfHeight),
        ], 90);

        // right
        yield return CreateRailFace([
            new Vector2(centerX + halfWidth, -halfHeight),
            new Vector2(centerX + halfWidth,  halfHeight),
        ], 90);
    }

    private static TrackRailProfileShape CreateRailFace(Vector2[] vertices, byte roughness)
    {
        return new TrackRailProfileShape(vertices, false) {
            Color = new TrackRgba8(149, 155, 158, 255),
            Specular = new TrackRailSpecularData(roughness, 255),
        };
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
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp60, Pitch.None, Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToFlat, Pitch.Slope60, Pitch.None));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25, -Pitch.Slope25, -Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60, -Pitch.Slope60, -Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown25, Pitch.None, -Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToDown60, -Pitch.Slope25, -Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToDown25, -Pitch.Slope60, -Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToFlat, -Pitch.Slope25, Pitch.None));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown60, Pitch.None, -Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToFlat, -Pitch.Slope60, Pitch.None));

        registry.Register(new LongBaseSlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp60LongBase, Pitch.None, Pitch.Slope60));
        registry.Register(new LongBaseSlopeTrackPieceBuilder(RCTTrackElementType.Up60ToFlatLongBase, Pitch.Slope60, Pitch.None));
        registry.Register(new LongBaseSlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown60LongBase, Pitch.None, -Pitch.Slope60));
        registry.Register(new LongBaseSlopeTrackPieceBuilder(RCTTrackElementType.Down60ToFlatLongBase, -Pitch.Slope60, Pitch.None));

        registry.RegisterMany(type => new CurveTrackPieceBuilder(type),
            RCTTrackElementType.LeftQuarterTurn5Tiles,
            RCTTrackElementType.RightQuarterTurn5Tiles,
            RCTTrackElementType.LeftQuarterTurn3Tiles,
            RCTTrackElementType.RightQuarterTurn3Tiles,
            RCTTrackElementType.SBendLeft,
            RCTTrackElementType.SBendRight,
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
            RCTTrackElementType.RightBankToFlat,
            RCTTrackElementType.BankedLeftQuarterTurn5Tiles,
            RCTTrackElementType.BankedRightQuarterTurn5Tiles,
            RCTTrackElementType.LeftBankedQuarterTurn3Tiles,
            RCTTrackElementType.RightBankedQuarterTurn3Tiles);

        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.LeftBankToUp25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.RightBankToUp25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Up25ToLeftBank));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Up25ToRightBank));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.LeftBankToDown25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.RightBankToDown25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Down25ToLeftBank));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Down25ToRightBank));

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
            RCTTrackElementType.BlockBrakes,
            RCTTrackElementType.OnRidePhoto);

        registry.RegisterMany(type => new DiagonalTrackPieceBuilder(type),
            RCTTrackElementType.DiagBrakes,
            RCTTrackElementType.DiagBlockBrakes,
            RCTTrackElementType.DiagFlat,
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
            RCTTrackElementType.DiagFlatToUp60LongBase,
            RCTTrackElementType.DiagUp60ToFlatLongBase,
            RCTTrackElementType.DiagFlatToDown60LongBase,
            RCTTrackElementType.DiagDown60ToFlatLongBase);
    }
}
