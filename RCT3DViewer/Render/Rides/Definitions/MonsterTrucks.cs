using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class MonsterTrucks : RideDefinition
{
    private static readonly TrackRgba8 metalColor = new(180, 180, 180, 255);
    private static readonly TrackRailSpecularData metalSpecular = new(160, 255);
    private static readonly TrackRailSpecularData roadSpecular = new(220, 0);

    public MonsterTrucks() : base(RCTRideType.MonsterTrucks)
    {
        SupportStyle = RideSupportStyle.MetalBoxed;

        CenterlineHeightOffset = 0.12f;
        SupportTrackOffset = -0.08f;
        CarVerticalOffset = 0.2f;
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var part = new TrackPart();

        // left
        part.RailProfiles.AddRange(CreateBox(-0.55f, -0.05f, 0.02f, 0.08f, shape => {
            shape.Color = metalColor;
            shape.Specular = metalSpecular;
        }));

        // center
        part.RailProfiles.AddRange(CreateBox( 0.00f, -0.05f, 0.02f, 0.08f, shape => {
            shape.Color = metalColor;
            shape.Specular = metalSpecular;
        }));

        // right
        part.RailProfiles.AddRange(CreateBox( 0.55f, -0.05f, 0.02f, 0.08f, shape => {
            shape.Color = metalColor;
            shape.Specular = metalSpecular;
        }));

        // top
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2( 0.55f, 0f),
            new Vector2(-0.55f, 0f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = roadSpecular,
        });

        // bottom
        part.RailProfiles.Add(new TrackRailProfileShape([
            new Vector2(-0.55f, -0.12f),
            new Vector2( 0.55f, -0.12f),
        ], false) {
            Swatch = TrackRailSwatch.Main,
            Specular = roadSpecular,
        });

        return part;
    }
}
