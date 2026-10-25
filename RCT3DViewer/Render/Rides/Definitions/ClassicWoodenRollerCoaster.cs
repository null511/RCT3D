using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class ClassicWoodenRollerCoaster : RideDefinition
{
    private const float RailHalfHeight = 0.03f;
    private const float SupportTopClearance = 0.02f;
    private const float RailSeparation = 0.64f;


    public ClassicWoodenRollerCoaster() : base(RCTRideType.ClassicWoodenRollerCoaster)
    {
        CenterlineHeightOffset = 0.8f;
        SupportStyle = RideSupportStyle.WoodenTruss;
        SupportTrackOffset = CenterlineHeightOffset - RailHalfHeight - SupportTopClearance;
    }

    protected override TrackRailProfile CreateProfile()
    {
        return new TrackRailProfile([
            .. CreateRail(-RailSeparation * 0.5f),
            .. CreateRail( RailSeparation * 0.5f),
        ]);
    }

    private static IEnumerable<TrackRailProfileShape> CreateRail(float centerX)
    {
        const float halfWidth = 0.04f;
        const float halfHeight = 0.03f;

        yield return CreateRailFace([
            new Vector2(centerX + halfWidth, halfHeight),
            new Vector2(centerX - halfWidth, halfHeight),
        ], 40);

        yield return CreateRailFace([
            new Vector2(centerX - halfWidth, halfHeight),
            new Vector2(centerX - halfWidth, -halfHeight),
        ], 90);

        yield return CreateRailFace([
            new Vector2(centerX - halfWidth, -halfHeight),
            new Vector2(centerX + halfWidth, -halfHeight),
        ], 90);

        yield return CreateRailFace([
            new Vector2(centerX + halfWidth, -halfHeight),
            new Vector2(centerX + halfWidth, halfHeight),
        ], 90);
    }

    private static TrackRailProfileShape CreateRailFace(Vector2[] vertices, byte roughness) =>
        new(vertices, false) {
            Color = new TrackRgba8(149, 155, 158, 255),
            Specular = new TrackRailSpecularData(roughness, 255),
        };

    protected override void BuildRegistry(TrackPieceBuilderRegistry registry)
    {
        registry.RegisterMany(type => new StraightTrackPieceBuilder(type),
            RCTTrackElementType.Flat,
            RCTTrackElementType.BeginStation,
            RCTTrackElementType.MiddleStation,
            RCTTrackElementType.EndStation,
            RCTTrackElementType.Brakes,
            RCTTrackElementType.BlockBrakes,
            RCTTrackElementType.Booster,
            RCTTrackElementType.OnRidePhoto);

        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25, Pitch.Slope25, Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60, Pitch.Slope60, Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp25, Pitch.None, Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToFlat, Pitch.Slope25, Pitch.None));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToUp60, Pitch.Slope25, Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToUp25, Pitch.Slope60, Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25, -Pitch.Slope25, -Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60, -Pitch.Slope60, -Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown25, Pitch.None, -Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToFlat, -Pitch.Slope25, Pitch.None));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToDown60, -Pitch.Slope25, -Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToDown25, -Pitch.Slope60, -Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp60, Pitch.None, Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToFlat, Pitch.Slope60, Pitch.None));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown60, Pitch.None, -Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToFlat, -Pitch.Slope60, Pitch.None));

        registry.Register(new LongBaseSlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp60LongBase, Pitch.None, Pitch.Slope60));
        registry.Register(new LongBaseSlopeTrackPieceBuilder(RCTTrackElementType.Up60ToFlatLongBase, Pitch.Slope60, Pitch.None));
        registry.Register(new LongBaseSlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown60LongBase, Pitch.None, -Pitch.Slope60));
        registry.Register(new LongBaseSlopeTrackPieceBuilder(RCTTrackElementType.Down60ToFlatLongBase, -Pitch.Slope60, Pitch.None));

        registry.RegisterMany(type => new CurveTrackPieceBuilder(type),
            RCTTrackElementType.LeftQuarterTurn3TilesUp25,
            RCTTrackElementType.RightQuarterTurn3TilesUp25,
            RCTTrackElementType.LeftQuarterTurn3TilesDown25,
            RCTTrackElementType.RightQuarterTurn3TilesDown25);

        // Three-tile sloped quarter turns are registered above to avoid duplicate registrations.
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
            RCTTrackElementType.RightQuarterTurn5TilesDown25);

        registry.RegisterMany(type => new BankedCurveTrackPieceBuilder(type),
            RCTTrackElementType.LeftBankedQuarterTurn3Tiles,
            RCTTrackElementType.RightBankedQuarterTurn3Tiles,
            RCTTrackElementType.LeftBankedQuarterTurn3TileUp25,
            RCTTrackElementType.RightBankedQuarterTurn3TileUp25,
            RCTTrackElementType.LeftBankedQuarterTurn3TileDown25,
            RCTTrackElementType.RightBankedQuarterTurn3TileDown25,
            RCTTrackElementType.LeftBankedQuarterTurn5TileUp25,
            RCTTrackElementType.RightBankedQuarterTurn5TileUp25,
            RCTTrackElementType.LeftBankedQuarterTurn5TileDown25,
            RCTTrackElementType.RightBankedQuarterTurn5TileDown25);

        registry.Register(new BankedCurveTrackPieceBuilder(RCTTrackElementType.RightEighthBankToDiag));
        registry.Register(new BankedCurveTrackPieceBuilder(RCTTrackElementType.RightEighthBankToOrthogonal));
        registry.Register(new BankedCurveTrackPieceBuilder(RCTTrackElementType.LeftEighthBankToDiag));

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
            RCTTrackElementType.RightBankToFlat,
            RCTTrackElementType.BankedLeftQuarterTurn5Tiles,
            RCTTrackElementType.BankedRightQuarterTurn5Tiles);

        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.LeftBankToUp25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.RightBankToUp25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Up25ToLeftBank));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Up25ToRightBank));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.LeftBankToDown25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.RightBankToDown25));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Down25ToLeftBank));
        registry.Register(new BankedSlopeTrackPieceBuilder(RCTTrackElementType.Down25ToRightBank));

        registry.RegisterMany(type => new VerticalLoopTrackPieceBuilder(type),
            RCTTrackElementType.LeftVerticalLoop,
            RCTTrackElementType.RightVerticalLoop);

        registry.RegisterMany(type => new MediumHalfLoopTrackPieceBuilder(type),
            RCTTrackElementType.LeftMediumHalfLoopUp,
            RCTTrackElementType.RightMediumHalfLoopUp,
            RCTTrackElementType.LeftMediumHalfLoopDown,
            RCTTrackElementType.RightMediumHalfLoopDown);

        registry.RegisterMany(type => new LargeHalfLoopTrackPieceBuilder(type),
            RCTTrackElementType.LeftLargeHalfLoopUp,
            RCTTrackElementType.RightLargeHalfLoopUp,
            RCTTrackElementType.LeftLargeHalfLoopDown,
            RCTTrackElementType.RightLargeHalfLoopDown);

        registry.RegisterMany(_ => new DiagonalFlatTrackPieceBuilder(),
            RCTTrackElementType.DiagFlat);

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
            RCTTrackElementType.DiagDown60ToFlat,
            RCTTrackElementType.DiagLeftBankToFlat,
            RCTTrackElementType.DiagFlatToRightBank,
            RCTTrackElementType.DiagBrakes,
            RCTTrackElementType.DiagBlockBrakes);
    }
}
