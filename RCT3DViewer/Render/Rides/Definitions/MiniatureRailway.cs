using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class MiniatureRailway : RideDefinition
{
    public MiniatureRailway() : base(RCTRideType.MiniatureRailway)
    {
        StationBuilder = new DefaultStationBuilder();
        SmoothMovement = true;
        CarVerticalOffset = 1.0f;

        SupportStyle = RideSupportStyle.WoodenTruss;
    }

    protected override IEnumerable<TrackRailProfileShape> GetRailParts(RCTTrackElementData track)
    {
        // rails
        yield return CreateRailRectangle(-0.40f, 0.16f);
        yield return CreateRailRectangle( 0.40f, 0.16f);

        if (!track.Part.IsStationPlatform()) {
            // ballast
            yield return new TrackRailProfileShape([
                new Vector2(1.2f, -0.04f),
                new Vector2(0.5f, 0.16f),

                new Vector2(0.0f, 0.18f),

                new Vector2(-0.5f, 0.16f),
                new Vector2(-1.2f, -0.04f),
            ], false) {
                Color = new TrackRgba8(130, 113, 87, 255),
            };
        }
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
