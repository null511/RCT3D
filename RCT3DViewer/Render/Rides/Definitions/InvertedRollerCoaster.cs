using System.Numerics;
using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class InvertedRollerCoaster : RideDefinition
{
    public InvertedRollerCoaster() : base(RCTRideType.InvertedRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 2.0f;
        CarVerticalOffset = -0.3f;

        SupportStyle = RideSupportStyle.MetalBoxed;
    }

    protected override TrackRailProfile CreateProfile()
    {
        const float railSeparation = 0.53f;
        const float railRadius = 0.07f;

        var railSpecular = new TrackRailSpecularData(100, 0);

        return new TrackRailProfile([
            new TrackRailProfileShape(CreateCircle(-railSeparation, 0.90f, railRadius, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = railSpecular,
            },
            new TrackRailProfileShape(CreateCircle( railSeparation, 0.90f, railRadius, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = railSpecular,
            },
            // main-top
            new TrackRailProfileShape([
                new Vector2( 0.30f, 1.60f),
                new Vector2(-0.30f, 1.60f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = railSpecular,
            },
            // main-bottom
            new TrackRailProfileShape([
                new Vector2(-0.30f, 1.10f),
                new Vector2( 0.30f, 1.10f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = railSpecular,
            },
            // main-left
            new TrackRailProfileShape([
                new Vector2(-0.30f, 1.60f),
                new Vector2(-0.30f, 1.10f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = railSpecular,
            },
            // main-right
            new TrackRailProfileShape([
                new Vector2( 0.30f, 1.10f),
                new Vector2( 0.30f, 1.60f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = railSpecular,
            },
        ]);
    }
}
