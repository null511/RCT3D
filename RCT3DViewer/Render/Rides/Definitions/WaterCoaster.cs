using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class WaterCoaster : RideDefinition
{
    private const float RailHalfSeparation = 0.42f;
    private const float RailRadius = 0.075f;
    private const float RailCenterY = 0.0f;

    private const float BraceRadius = 0.055f;

    private static readonly TrackRailSpecularData railSpecular = new(80, 80);


    public WaterCoaster() : base(RCTRideType.WaterCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.25f;
        //Vehicles.LocalOffset = 0.20f;

        SupportStyle = RideSupportStyle.MetalFork;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var part = new TrackPart {
            BraceInterval = 0.8f,
        };

        part.AppendRailPart.Circle(-RailHalfSeparation, RailCenterY, RailRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        part.AppendRailPart.Circle(RailHalfSeparation, RailCenterY, RailRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        part.AppendBracePart.Cylinder(
            new Vector3(-RailHalfSeparation, RailCenterY, 0f),
            new Vector3( RailHalfSeparation, RailCenterY, 0f),
            BraceRadius, 5, shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = railSpecular;
            });

        if (track.HasChainLift != 0) {
            // chain
            part.AppendRailPart.Box(0f, 0.05f, 0.05f, 0.03f, shape => {
                shape.Color = new TrackRgba8(30, 30, 30, 255);
                shape.Specular = new TrackRailSpecularData(100, 255);
            });
        }
        else if (track.Part.IsCovered()) {
            part.WaterPlane = [
                new Vector2(-1.0f, 0.1f),
                new Vector2( 1.0f, 0.1f),
            ];

            // top
            part.RailProfiles.Add(new TrackRailProfileShape([
                new Vector2( 1.00f,  0.25f),
                new Vector2( 0.90f, -0.20f),
                new Vector2(-0.90f, -0.20f),
                new Vector2(-1.00f,  0.25f),
            ], false) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = railSpecular,
            });

            // bottom
            part.RailProfiles.Add(new TrackRailProfileShape([
                new Vector2(-1.02f,  0.23f),
                new Vector2(-0.92f, -0.22f),
                new Vector2( 0.92f, -0.22f),
                new Vector2( 1.02f,  0.23f),
            ], false) {
                Swatch = TrackRailSwatch.Main,
                Specular = railSpecular,
            });

            // left
            part.RailProfiles.Add(new TrackRailProfileShape([
                new Vector2(-1.00f,  0.25f),
                new Vector2(-1.02f,  0.23f),
            ], false) {
                Swatch = TrackRailSwatch.Main,
                Specular = railSpecular,
            });

            // right
            part.RailProfiles.Add(new TrackRailProfileShape([
                new Vector2( 1.02f,  0.23f),
                new Vector2( 1.00f,  0.25f),
            ], false) {
                Swatch = TrackRailSwatch.Main,
                Specular = railSpecular,
            });
        }

        return part;
    }
}
