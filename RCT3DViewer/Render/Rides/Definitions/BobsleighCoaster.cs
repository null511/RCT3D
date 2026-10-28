using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class BobsleighCoaster : RideDefinition
{
    public BobsleighCoaster() : base(RCTRideType.BobsleighCoaster)
    {
        StationBuilder = new DefaultStationBuilder();
        //SmoothMovement = true;
        CenterlineHeightOffset = 0.65f;
        //CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = -0.45f;
    }

    protected override IEnumerable<TrackRailProfileShape> GetRailParts(RCTTrackElementData track)
    {
        return track.HasChainLift != 0 || track.Part.IsStationPlatform()
            ? GetTrackedRailParts(track.Part) : GetBeddedRailParts();
    }

    private IEnumerable<TrackRailProfileShape> GetTrackedRailParts(RCTTrackElementType part)
    {
        var specularRails = new TrackRailSpecularData(120, 10);

        // lower
        yield return new TrackRailProfileShape(CreateCircle(-0.32f, -0.50f, 0.05f, 4), true) {
            Swatch = TrackRailSwatch.Main,
            Specular = specularRails,
        };
        yield return new TrackRailProfileShape(CreateCircle( 0.32f, -0.50f, 0.05f, 4), true) {
            Swatch = TrackRailSwatch.Main,
            Specular = specularRails,
        };

        if (!part.IsStationPlatform()) {
            // upper-left
            yield return new TrackRailProfileShape(CreateCircle(-0.74f, -0.15f, 0.03f, 4), true) {
                Swatch = TrackRailSwatch.Main,
                Specular = specularRails,
            };

            // upper-right
            yield return new TrackRailProfileShape(CreateCircle( 0.74f, -0.15f, 0.03f, 4), true) {
                Swatch = TrackRailSwatch.Main,
                Specular = specularRails,
            };
        }
    }

    private IEnumerable<TrackRailProfileShape> GetBeddedRailParts()
    {
        var specularRails = new TrackRailSpecularData(120, 10);
        var specularMeshing = new TrackRailSpecularData(200, 0);

        // supports
        yield return new TrackRailProfileShape(CreateCircle(-0.88f, 0.40f, 0.05f, 4), true) {
            Swatch = TrackRailSwatch.Alternate,
            Specular = specularRails,
        };
        yield return new TrackRailProfileShape(CreateCircle(-0.32f, -0.50f, 0.05f, 4), true) {
            Swatch = TrackRailSwatch.Alternate,
            Specular = specularRails,
        };
        yield return new TrackRailProfileShape(CreateCircle(0.32f, -0.50f, 0.05f, 4), true) {
            Swatch = TrackRailSwatch.Alternate,
            Specular = specularRails,
        };
        yield return new TrackRailProfileShape(CreateCircle(0.88f, 0.40f, 0.05f, 4), true) {
            Swatch = TrackRailSwatch.Alternate,
            Specular = specularRails,
        };

        // bed up
        yield return new TrackRailProfileShape([
            new Vector2(0.88f, 0.40f),
            new Vector2(0.70f, -0.14f),
            new Vector2(0.32f, -0.44f),
            new Vector2(-0.32f, -0.44f),
            new Vector2(-0.70f, -0.14f),
            new Vector2(-0.88f, 0.40f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = specularMeshing,
        };

        // bed down
        yield return new TrackRailProfileShape([
            new Vector2(-0.91f,  0.40f),
            new Vector2(-0.72f, -0.15f),
            new Vector2(-0.33f, -0.47f),
            new Vector2( 0.33f, -0.47f),
            new Vector2( 0.72f, -0.15f),
            new Vector2( 0.91f,  0.40f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = specularMeshing,
        };
    }
}
