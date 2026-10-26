using RCT3D.Interop;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class MineTrainCoaster : RideDefinition
{
    public MineTrainCoaster() : base(RCTRideType.MineTrainCoaster)
    {
        CenterlineHeightOffset = 0.2f;
        CarVerticalOffset = 0.5f;
    }

    protected override TrackRailProfile CreateProfile()
    {
        const float RailSeparation = 0.48f;

        return new TrackRailProfile([
            .. CreateRail(-RailSeparation*0.5f),
            .. CreateRail( RailSeparation*0.5f),
        ]);
    }

    private static IEnumerable<TrackRailProfileShape> CreateRail(float centerX)
    {
        const float halfWidth = 0.02f;
        const float halfHeight = 0.03f;

        // top
        yield return CreateRailFace([
            new Vector2(centerX + halfWidth, halfHeight),
            new Vector2(centerX - halfWidth, halfHeight),
        ], 40);

        // left
        yield return CreateRailFace([
            new Vector2(centerX - halfWidth,  halfHeight),
            new Vector2(centerX - halfWidth, -halfHeight),
        ], 90);

        // bottom
        yield return CreateRailFace([
            new Vector2(centerX - halfWidth, -halfHeight),
            new Vector2(centerX + halfWidth, -halfHeight),
        ], 90);

        // right
        yield return CreateRailFace([
            new Vector2(centerX + halfWidth, -halfHeight),
            new Vector2(centerX + halfWidth,  halfHeight),
        ], 90);
    }

    private static TrackRailProfileShape CreateRailFace(Vector2[] vertices, byte roughness)
    {
        return new TrackRailProfileShape(vertices, false) {
            Color = new TrackRgba8(149, 155, 158, 255),
            Specular = new TrackRailSpecularData(roughness, 255),
        };
    }
}
