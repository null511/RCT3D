using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class MiniatureRailway : RideDefinition
{
    private static readonly TrackRgba8 WoodColor = new(128, 107, 76, 255);
    private static readonly TrackRailSpecularData WoodSpecular = new(250, 0);


    public MiniatureRailway() : base(RCTRideType.MiniatureRailway)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.25f;
        CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.WoodenTruss;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var shape = new TrackPart {
            BraceInterval = 0.5f,
            RailProfiles = [
                CreateRailRectangle(-0.40f, 0f),
                CreateRailRectangle( 0.40f, 0f),
            ],
        };

        if (!track.Part.IsStationPlatform()) {
            // ballast
            shape.RailProfiles.Add(new TrackRailProfileShape([
                new Vector2( 1.2f, -0.28f),
                new Vector2( 0.5f, -0.08f),

                new Vector2( 0.0f, -0.06f),

                new Vector2(-0.5f, -0.08f),
                new Vector2(-1.2f, -0.28f),
            ], false) {
                Color = new TrackRgba8(130, 113, 87, 255),
            });

            // ties
            shape.AppendBracePart.Box(
                new Vector3(0f, -0.05f, 0f),
                new Vector3(0.64f, 0.05f, 0.08f),
                p => {
                    p.Color = WoodColor;
                    p.Specular = WoodSpecular;
                });
        }

        return shape;
    }

    private static TrackRailProfileShape CreateRailRectangle(float centerX, float baseY)
    {
        const float halfWidth = 0.042f;
        const float height = 0.11f;
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
        ], false) {
            Color = new TrackRgba8(187, 200, 209, 255),
            Specular = new TrackRailSpecularData(80, 255),
        };
    }
}
