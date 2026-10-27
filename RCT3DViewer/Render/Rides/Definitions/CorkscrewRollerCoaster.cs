using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Rides.Definitions;

internal class CorkscrewRollerCoaster : RideDefinition
{
    public CorkscrewRollerCoaster() : base(RCTRideType.CorkscrewRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.5f;
        CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = -0.24f;
    }

    protected override IEnumerable<TrackRailProfileShape> GetRailParts(RCTTrackElementType elementType)
    {
        const float railSeparation = 0.42f;

        return [
            new TrackRailProfileShape(CreateCircle(-railSeparation, 0f, 0.08f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(100, 0),
            },
            new TrackRailProfileShape(CreateCircle(0f, -0.24f, 0.14f, 6), true) {
                Swatch = TrackRailSwatch.Main,
                Specular = new TrackRailSpecularData(100, 0),
            },
            new TrackRailProfileShape(CreateCircle(railSeparation, 0f, 0.08f, 5), true) {
                Swatch = TrackRailSwatch.Alternate,
                Specular = new TrackRailSpecularData(100, 0),
            },
        ];
    }
}
