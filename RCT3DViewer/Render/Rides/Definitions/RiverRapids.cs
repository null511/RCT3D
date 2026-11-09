using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class RiverRapids : RideDefinition
{
    private static readonly TrackRailSpecularData specular = new(220, 0);


    public RiverRapids() : base(RCTRideType.RiverRapids)
    {
        CenterlineHeightOffset = 0.75f;

        SupportStyle = RideSupportStyle.WoodenTruss;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var part = new TrackPart {
            WaterPlane = [
                new Vector2(-1.46f, 0.7f),
                new Vector2( 1.46f, 0.7f),
            ],
        };

        const float thickness = 0.04f;

        const float sideCenterX = 1.46f;
        const float sideCenterY = 0f;

        const float bottomCenterX = 0f;
        const float bottomCenterY = -0.75f + thickness * 0.5f;

        // left side
        part.AppendRailPart.Box(-sideCenterX, sideCenterY, thickness, 0.75f, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = specular;
        });

        // right side
        part.AppendRailPart.Box(sideCenterX, sideCenterY, thickness, 0.75f, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = specular;
        });

        // bottom
        part.AppendRailPart.Box(bottomCenterX, bottomCenterY, 1.46f, thickness, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = specular;
        });

        return part;
    }
}
