using RCT3D.Interop;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class BobsleighCoaster : RideDefinition
{
    public BobsleighCoaster() : base(RCTRideType.BobsleighCoaster)
    {
        //SmoothMovement = true;
        CenterlineHeightOffset = 0.6f;
        //CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = -0.45f;
    }

    protected override TrackRailProfile CreateProfile()
    {
        var specularRails = new TrackRailSpecularData(120, 10);
        var specularMeshing = new TrackRailSpecularData(200, 0);

        return new TrackRailProfile([
            // supports
            new TrackRailProfileShape(CreateCircle(-0.88f, 0.40f, 0.05f, 4), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = specularRails,
            },
            new TrackRailProfileShape(CreateCircle(-0.32f, -0.50f, 0.05f, 4), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = specularRails,
            },
            new TrackRailProfileShape(CreateCircle( 0.32f, -0.50f, 0.05f, 4), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = specularRails,
            },
            new TrackRailProfileShape(CreateCircle( 0.88f, 0.40f, 0.05f, 4), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = specularRails,
            },

            // bed up
            new TrackRailProfileShape([
                new Vector2( 0.88f,  0.40f),
                new Vector2( 0.70f, -0.14f),
                new Vector2( 0.32f, -0.44f),
                new Vector2(-0.32f, -0.44f),
                new Vector2(-0.70f, -0.14f),
                new Vector2(-0.88f,  0.40f),
            ], false) {
                Swatch = TrackRailSwatch.Main,
                Specular = specularMeshing,
            },

            // bed down
            new TrackRailProfileShape([
                new Vector2(-0.91f,  0.40f),
                new Vector2(-0.72f, -0.15f),
                new Vector2(-0.33f, -0.47f),
                new Vector2( 0.33f, -0.47f),
                new Vector2( 0.72f, -0.15f),
                new Vector2( 0.91f,  0.40f),
            ], false) {
                Swatch = TrackRailSwatch.Main,
                Specular = specularMeshing,
            },
        ]);
    }
}
