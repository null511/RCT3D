using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class BobsleighCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData specularRails = new(120, 10);
    private static readonly TrackRailSpecularData specularMeshing = new(200, 0);


    public BobsleighCoaster() : base(RCTRideType.BobsleighCoaster)
    {
        StationBuilder = new DefaultTrackStationBuilder();
        //SmoothMovement = true;
        CenterlineHeightOffset = 0.65f;
        //CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = -0.45f;
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        return track.HasChainLift != 0 || track.Part.IsStationPlatform()
            ? GetTrackedRailParts(track.Part) : GetBeddedRailParts();
    }

    private TrackPart GetTrackedRailParts(RCTTrackElementType track)
    {
        var part = new TrackPart();

        // lower
        part.AppendRailPart.Circle(-0.32f, -0.50f, 0.05f, 4, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = specularRails;
        });

        part.AppendRailPart.Circle( 0.32f, -0.50f, 0.05f, 4, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = specularRails;
        });

        if (!track.IsStationPlatform()) {
            // upper-left
            part.AppendRailPart.Circle(-0.74f, -0.15f, 0.03f, 4, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = specularRails;
            });

            // upper-right
            part.AppendRailPart.Circle( 0.74f, -0.15f, 0.03f, 4, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = specularRails;
            });
        }

        return part;
    }

    private TrackPart GetBeddedRailParts()
    {
        var part = new TrackPart();

        // supports
        part.AppendRailPart.Circle(-0.88f, 0.40f, 0.05f, 4, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = specularRails;
        });

        part.AppendRailPart.Circle(-0.32f, -0.50f, 0.05f, 4, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = specularRails;
        });

        part.AppendRailPart.Circle(0.32f, -0.50f, 0.05f, 4, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = specularRails;
        });

        part.AppendRailPart.Circle(0.88f, 0.40f, 0.05f, 4, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = specularRails;
        });

        // bed up
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(0.88f, 0.40f),
            new Vector2(0.70f, -0.14f),
            new Vector2(0.32f, -0.44f),
            new Vector2(-0.32f, -0.44f),
            new Vector2(-0.70f, -0.14f),
            new Vector2(-0.88f, 0.40f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = specularMeshing,
        });

        // bed down
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(-0.91f,  0.40f),
            new Vector2(-0.72f, -0.15f),
            new Vector2(-0.33f, -0.47f),
            new Vector2( 0.33f, -0.47f),
            new Vector2( 0.72f, -0.15f),
            new Vector2( 0.91f,  0.40f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = specularMeshing,
        });

        return part;
    }
}
