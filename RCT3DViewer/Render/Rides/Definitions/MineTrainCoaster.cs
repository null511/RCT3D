using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class MineTrainCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData WoodSpecular = new(250, 0);


    public MineTrainCoaster() : base(RCTRideType.MineTrainCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.2f;
        CarVerticalOffset = 0.5f;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        const float RailSeparation = 0.48f;

        var shape = new TrackPart {
            BraceInterval = 0.36f,
        };

        shape.RailProfiles.AddRange(CreateRail(-RailSeparation*0.5f));
        shape.RailProfiles.AddRange(CreateRail( RailSeparation*0.5f));

        if (!track.Part.IsStationPlatform()) {
            shape.AppendBracePart.Box(
                new Vector3(0f, -0.044f, 0f),
                new Vector3(0.55f, 0.014f, 0.06f),
                part => {
                    part.Swatch = TrackRailSwatch.Main;
                    part.Specular = WoodSpecular;
                });
        }

        return shape;
    }

    private static IEnumerable<TrackRailProfileShape> CreateRail(float centerX)
    {
        const float halfWidth = 0.02f;
        const float halfHeight = 0.03f;

        return [
            // top
            CreateRailFace([
                new Vector2(centerX + halfWidth, halfHeight),
                new Vector2(centerX - halfWidth, halfHeight),
            ], 40),

            // left
            CreateRailFace([
            new Vector2(centerX - halfWidth, halfHeight),
            new Vector2(centerX - halfWidth, -halfHeight),
            ], 90),

            // bottom
            CreateRailFace([
            new Vector2(centerX - halfWidth, -halfHeight),
            new Vector2(centerX + halfWidth, -halfHeight),
            ], 90),

            // right
            CreateRailFace([
            new Vector2(centerX + halfWidth, -halfHeight),
            new Vector2(centerX + halfWidth, halfHeight),
            ], 90),
        ];
    }

    private static TrackRailProfileShape CreateRailFace(Vector2[] vertices, byte roughness)
    {
        return new TrackRailProfileShape(vertices, false) {
            Color = new TrackRgba8(149, 155, 158, 255),
            Specular = new TrackRailSpecularData(roughness, 255),
        };
    }
}
