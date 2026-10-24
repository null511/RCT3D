using RCT3D.Interop;
using RCT3D.Render.Tracks.Meshing;
using System.Numerics;

namespace RCT3D.Render.Tracks.Rides;

internal class WoodenWildMouse() : RideDefinition(RCTRideType.WoodenWildMouse)
{
    protected override RideSupportStyle SupportStyle => RideSupportStyle.WoodenTruss;

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
        registry.RegisterMany(type => new StraightTrackPieceBuilder(type, 0.0f),
            RCTTrackElementType.Flat,
            RCTTrackElementType.EndStation,
            RCTTrackElementType.BeginStation,
            RCTTrackElementType.MiddleStation);

        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25, TrackCenterlineGeometry.Pitch25Slope, TrackCenterlineGeometry.Pitch25Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25, -TrackCenterlineGeometry.Pitch25Slope, -TrackCenterlineGeometry.Pitch25Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp25, 0.0f, TrackCenterlineGeometry.Pitch25Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToFlat, TrackCenterlineGeometry.Pitch25Slope, 0.0f));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown25, 0.0f, -TrackCenterlineGeometry.Pitch25Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToFlat, -TrackCenterlineGeometry.Pitch25Slope, 0.0f));

        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60, TrackCenterlineGeometry.Pitch60Slope, TrackCenterlineGeometry.Pitch60Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToUp60, TrackCenterlineGeometry.Pitch25Slope, TrackCenterlineGeometry.Pitch60Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToUp25, TrackCenterlineGeometry.Pitch60Slope, TrackCenterlineGeometry.Pitch25Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60, -TrackCenterlineGeometry.Pitch60Slope, -TrackCenterlineGeometry.Pitch60Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToDown60, -TrackCenterlineGeometry.Pitch25Slope, -TrackCenterlineGeometry.Pitch60Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToDown25, -TrackCenterlineGeometry.Pitch60Slope, -TrackCenterlineGeometry.Pitch25Slope));

        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp60, 0.0f, TrackCenterlineGeometry.Pitch60Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToFlat, TrackCenterlineGeometry.Pitch60Slope, 0.0f));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown60, 0.0f, -TrackCenterlineGeometry.Pitch60Slope));
        registry.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToFlat, -TrackCenterlineGeometry.Pitch60Slope, 0.0f));

        registry.RegisterMany(type => new CurveTrackPieceBuilder(type),
            RCTTrackElementType.LeftQuarterTurn1Tile,
            RCTTrackElementType.RightQuarterTurn1Tile,
            RCTTrackElementType.LeftQuarterTurn3Tiles,
            RCTTrackElementType.RightQuarterTurn3Tiles);
    }
}
