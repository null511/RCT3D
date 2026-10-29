using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class WoodenRollerCoaster : RideDefinition
{
    private const float SupportTopClearance = 0.02f;

    private const float RailHalfSeparation = 0.72f;
    private const float RailHalfWidth = 0.08f;
    private const float RailHalfHeight = 0.03f;
    private const float RailWoodInset = 0.2f;


    public WoodenRollerCoaster() : base(RCTRideType.WoodenRollerCoaster)
    {
        //SmoothMovement = true;
        //TrackBankAngleDegrees = 45.0f;
        CenterlineHeightOffset = 0.65f;
        CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.WoodenTruss;
        SupportTrackOffset = CenterlineHeightOffset - RailHalfHeight - SupportTopClearance;

        StationBuilder = new DefaultStationBuilder();
    }

    protected override IEnumerable<TrackRailProfileShape> GetRailParts(RCTTrackElementData track)
    {
        foreach (var part in CreateRail(-RailHalfSeparation, -0.2f)) yield return part;
        foreach (var part in CreateRail( RailHalfSeparation, -0.2f)) yield return part;

        if (!track.Part.IsStationPlatform() && !track.Part.IsLoop()) {
            const float woodHalfMargin = (1.5f - (RailHalfSeparation - RailWoodInset)) * 0.5f;
            const float woodHeight = 0.06f;

            const float woodPlatformCenterX = 1.5f - woodHalfMargin;
            const float woodPlatformCenterY = -0.26f;

            // side platforms
            foreach (var part in CreateWood(-woodPlatformCenterX, woodPlatformCenterY, woodHalfMargin, woodHeight)) yield return part;
            foreach (var part in CreateWood( woodPlatformCenterX, woodPlatformCenterY, woodHalfMargin, woodHeight)) yield return part;

            const float WoodRailingWidth = 0.04f;

            var woodRailingCenterX = 1.5f - 0.5f*WoodRailingWidth;
            var woodRailingCenterY = 0.36f;

            // side railing
            foreach (var part in CreateWood(-woodRailingCenterX, woodRailingCenterY, WoodRailingWidth, WoodRailingWidth)) yield return part;
            foreach (var part in CreateWood( woodRailingCenterX, woodRailingCenterY, WoodRailingWidth, WoodRailingWidth)) yield return part;
        }
    }

    private static IEnumerable<TrackRailProfileShape> CreateRail(float centerX, float centerY)
    {
        return CreateBox(centerX, centerY, RailHalfWidth, RailHalfHeight, shape => {
            shape.Color = new TrackRgba8(149, 155, 158, 255);
            shape.Specular = new TrackRailSpecularData(80, 255);
        });
    }

    private static IEnumerable<TrackRailProfileShape> CreateWood(float centerX, float centerY, float halfWidth, float halfHeight)
    {
        return CreateBox(centerX, centerY, halfWidth, halfHeight, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = new TrackRailSpecularData(220, 0);
        });
    }
}
