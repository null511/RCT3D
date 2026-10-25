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

}
