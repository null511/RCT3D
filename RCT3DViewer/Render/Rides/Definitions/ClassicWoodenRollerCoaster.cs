using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class ClassicWoodenRollerCoaster : RideDefinition
{
    private const float RailHalfHeight = 0.03f;
    private const float SupportTopClearance = 0.02f;
    private const float RailSeparation = 0.64f;


    public ClassicWoodenRollerCoaster() : base(RCTRideType.ClassicWoodenRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.25f;
        CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.WoodenTruss;
        SupportTrackOffset = CenterlineHeightOffset - RailHalfHeight - SupportTopClearance;
    }

    protected override IEnumerable<TrackRailProfileShape> GetRailParts(RCTTrackElementType elementType)
    {
        return [
            .. CreateRail(-RailSeparation * 0.5f),
            .. CreateRail( RailSeparation * 0.5f),
        ];
    }

    private static IEnumerable<TrackRailProfileShape> CreateRail(float centerX)
    {
        const float halfWidth = 0.04f;
        const float halfHeight = 0.03f;

        yield return CreateRailFace([
            new Vector2(centerX + halfWidth, halfHeight),
            new Vector2(centerX - halfWidth, halfHeight),
        ], 40);

        yield return CreateRailFace([
            new Vector2(centerX - halfWidth, halfHeight),
            new Vector2(centerX - halfWidth, -halfHeight),
        ], 90);

        yield return CreateRailFace([
            new Vector2(centerX - halfWidth, -halfHeight),
            new Vector2(centerX + halfWidth, -halfHeight),
        ], 90);

        yield return CreateRailFace([
            new Vector2(centerX + halfWidth, -halfHeight),
            new Vector2(centerX + halfWidth, halfHeight),
        ], 90);
    }

    private static TrackRailProfileShape CreateRailFace(Vector2[] vertices, byte roughness) =>
        new(vertices, false) {
            Color = new TrackRgba8(149, 155, 158, 255),
            Specular = new TrackRailSpecularData(roughness, 255),
        };
}
