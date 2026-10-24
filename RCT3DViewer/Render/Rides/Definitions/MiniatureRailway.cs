using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class MiniatureRailway : RideDefinition
{
    public MiniatureRailway() : base(RCTRideType.MiniatureRailway)
    {
        SupportStyle = RideSupportStyle.WoodenTruss;
    }

    protected override TrackRailProfile CreateProfile()
    {
        return new TrackRailProfile([
            CreateRailRectangle(-0.42f, 0.08f),
            CreateRailRectangle( 0.42f, 0.08f),
            new TrackRailProfileShape([
                    new Vector2( 0.9f, -0.04f),
                    new Vector2( 0.5f,  0.08f),

                    new Vector2( 0.0f,  0.10f),

                    new Vector2(-0.5f,  0.08f),
                    new Vector2(-0.9f, -0.04f),
                ], false) {
                Color = new TrackRgba8(130, 113, 87, 255),
            },
        ]);
    }

    private static TrackRailProfileShape CreateRailRectangle(float centerX, float baseY)
    {
        const float halfWidth = 0.06f;
        const float height = 0.14f;
        const float radius = 0.014f;

        return new TrackRailProfileShape([
                new Vector2(centerX + halfWidth, baseY),

                new Vector2(centerX + halfWidth, baseY + height-2f*radius),
                new Vector2(centerX + halfWidth, baseY + height-radius),
                new Vector2(centerX + halfWidth-radius, baseY + height),
                new Vector2(centerX + halfWidth-2f*radius, baseY + height),

                new Vector2(centerX - halfWidth+2f*radius, baseY + height),
                new Vector2(centerX - halfWidth+radius, baseY + height),
                new Vector2(centerX - halfWidth, baseY + height-radius),
                new Vector2(centerX - halfWidth, baseY + height-2f*radius),

                new Vector2(centerX - halfWidth, baseY),
            ], false)
        {
            Color = new TrackRgba8(187, 200, 209, 255),
            Specular = new TrackRailSpecularData(80, 255),
        };
    }

    protected override void BuildRegistry(TrackPieceBuilderRegistry registry)
    {
        registry.RegisterMany(type => new StraightTrackPieceBuilder(type),
            RCTTrackElementType.Flat,
            RCTTrackElementType.EndStation,
            RCTTrackElementType.BeginStation,
            RCTTrackElementType.MiddleStation);

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
            RCTTrackElementType.RightEighthBankToOrthogonal);
    }
}
