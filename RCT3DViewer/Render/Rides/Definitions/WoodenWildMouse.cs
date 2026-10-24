using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class WoodenWildMouse : RideDefinition
{
    public WoodenWildMouse() : base(RCTRideType.WoodenWildMouse)
    {
        SupportStyle = RideSupportStyle.WoodenTruss;
    }

    protected override TrackRailProfile CreateProfile()
    {
        const float centerY = 0.2f;

        const float centerHalfWidth = 0.22f;
        const float centerHalfHeight = 0.06f;

        var color = new TrackRgba8(255, 255, 255, 255);
        var specular = new TrackRailSpecularData(180, 255);

        return new TrackRailProfile([
            new TrackRailProfileShape([
                new Vector2(-centerHalfWidth, centerY + centerHalfHeight),
                new Vector2( centerHalfWidth, centerY + centerHalfHeight),
            ], false) { Color = color, Specular = specular },
            new TrackRailProfileShape([
                new Vector2( centerHalfWidth, centerY - centerHalfHeight),
                new Vector2(-centerHalfWidth, centerY - centerHalfHeight),
            ], false) { Color = color, Specular = specular },
            new TrackRailProfileShape([
                new Vector2(-centerHalfWidth, centerY - centerHalfHeight),
                new Vector2(-centerHalfWidth, centerY + centerHalfHeight),
            ], false) { Color = color, Specular = specular },
            new TrackRailProfileShape([
                new Vector2( centerHalfWidth, centerY + centerHalfHeight),
                new Vector2( centerHalfWidth, centerY - centerHalfHeight),
            ], false) { Color = color, Specular = specular },
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
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25, -Pitch.Slope25, -Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp25, Pitch.None, Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToFlat, Pitch.Slope25, Pitch.None));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown25, Pitch.None, -Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToFlat, -Pitch.Slope25, Pitch.None));

        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60, Pitch.Slope60, Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToUp60, Pitch.Slope25, Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToUp25, Pitch.Slope60, Pitch.Slope25));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60, -Pitch.Slope60, -Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToDown60, -Pitch.Slope25, -Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToDown25, -Pitch.Slope60, -Pitch.Slope25));

        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp60, Pitch.None, Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToFlat, Pitch.Slope60, Pitch.None));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown60, Pitch.None, -Pitch.Slope60));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToFlat, -Pitch.Slope60, Pitch.None));

        registry.RegisterMany(type => new CurveTrackPieceBuilder(type),
            RCTTrackElementType.LeftQuarterTurn1Tile,
            RCTTrackElementType.RightQuarterTurn1Tile,
            RCTTrackElementType.LeftQuarterTurn3Tiles,
            RCTTrackElementType.RightQuarterTurn3Tiles);
    }
}
