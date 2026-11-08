using System.Numerics;
using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;

namespace RCT3D.Render.Rides.Definitions;

internal sealed class SideFrictionRollerCoaster : RideDefinition
{
    private const float SideRailHalfSeparation = 0.85f;
    private const float SideRailCenterY = 0.0f;

    private const float BottomRailHalfSeparation = 0.55f;
    private const float BottomRailCenterY = -0.2f;

    private const float BeamHalfSize = 0.06f;

    //private const float RailHalfWidth = 0.08f;
    //private const float RailHalfHeight = 0.03f;
    //private const float RailWoodInset = 0.2f;

    private static readonly TrackRgba8 RailColor = new(187, 193, 196, 255);
    private static readonly TrackRailSpecularData RailSpecular = new(80, 255);
    private static readonly TrackRailSpecularData WoodSpecular = new(220, 0);


    public SideFrictionRollerCoaster() : base(RCTRideType.SideFrictionRollerCoaster)
    {
        CenterlineHeightOffset = 0.5f;

        SupportStyle = RideSupportStyle.WoodenTruss;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var part = new TrackPart {
            BraceInterval = 0.6f,
        };

        // bottom left rail
        part.RailProfiles.AddRange(CreateBox(-BottomRailHalfSeparation, BottomRailCenterY, 0.08f, 0.03f, shape => {
            shape.Color = RailColor;
            shape.Specular = RailSpecular;
        }));

        // bottom right rail
        part.RailProfiles.AddRange(CreateBox( BottomRailHalfSeparation, BottomRailCenterY, 0.08f, 0.03f, shape => {
            shape.Color = RailColor;
            shape.Specular = RailSpecular;
        }));

        if (!track.Part.IsStationPlatform() && !track.Part.IsLoop()) {
            // side left rail
            part.RailProfiles.AddRange(CreateBox(-SideRailHalfSeparation, SideRailCenterY, 0.02f, 0.12f, shape => {
                shape.Color = RailColor;
                shape.Specular = RailSpecular;
            }));

            // side right rail
            part.RailProfiles.AddRange(CreateBox( SideRailHalfSeparation, SideRailCenterY, 0.02f, 0.12f, shape => {
                shape.Color = RailColor;
                shape.Specular = RailSpecular;
            }));

            const float baseTop = -0.32f;
            var beamBottom = baseTop - BeamHalfSize * 2f;

            var beamSize = 2f * BeamHalfSize;
            var diagSize = MathF.Sqrt(beamSize * beamSize * 2f);
            var diagSize2 = 2f * diagSize;

            // base beam
            part.AppendBracePart.Box([
                new Vector3(-1.50f + diagSize, beamBottom, -BeamHalfSize),
                new Vector3( 1.50f - diagSize, beamBottom, -BeamHalfSize),
                new Vector3( 1.50f - diagSize, beamBottom,  BeamHalfSize),
                new Vector3(-1.50f + diagSize, beamBottom,  BeamHalfSize),

                new Vector3(-1.50f + diagSize2, baseTop, -BeamHalfSize),
                new Vector3( 1.50f - diagSize2, baseTop, -BeamHalfSize),
                new Vector3( 1.50f - diagSize2, baseTop,  BeamHalfSize),
                new Vector3(-1.50f + diagSize2, baseTop,  BeamHalfSize),
            ],
            shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = WoodSpecular;
            });

            var margin = 1.5f - (SideRailHalfSeparation + beamSize);
            var sideBraceTop = beamBottom + margin;

            // left diagonal beam
            part.AppendBracePart.Box([
                new Vector3(-1.5f,            beamBottom, -BeamHalfSize),
                new Vector3(-1.5f + diagSize, beamBottom, -BeamHalfSize),
                new Vector3(-1.5f + diagSize, beamBottom,  BeamHalfSize),
                new Vector3(-1.5f,            beamBottom,  BeamHalfSize),

                new Vector3(-1.5f + margin, sideBraceTop,            -BeamHalfSize),
                new Vector3(-1.5f + margin, sideBraceTop - diagSize, -BeamHalfSize),
                new Vector3(-1.5f + margin, sideBraceTop - diagSize,  BeamHalfSize),
                new Vector3(-1.5f + margin, sideBraceTop,             BeamHalfSize),
            ],
            shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = WoodSpecular;
            });

            // right diagonal beam
            part.AppendBracePart.Box([
                new Vector3(1.5f - diagSize, beamBottom, -BeamHalfSize),
                new Vector3(1.5f,            beamBottom, -BeamHalfSize),
                new Vector3(1.5f,            beamBottom,  BeamHalfSize),
                new Vector3(1.5f - diagSize, beamBottom,  BeamHalfSize),

                new Vector3(1.5f - margin, sideBraceTop - diagSize, -BeamHalfSize),
                new Vector3(1.5f - margin, sideBraceTop,            -BeamHalfSize),
                new Vector3(1.5f - margin, sideBraceTop,             BeamHalfSize),
                new Vector3(1.5f - margin, sideBraceTop - diagSize,  BeamHalfSize),
            ],
            shape => {
                shape.Swatch = TrackRailSwatch.Main;
                shape.Specular = WoodSpecular;
            });

            var sideBraceOffset = SideRailHalfSeparation + BeamHalfSize;
            var sideBraceHalfHeight = 0.5f * (margin - beamSize);
            var sideBraceCenterY = baseTop + sideBraceHalfHeight;

            // left vertical beam
            part.AppendBracePart.Box(
                new Vector3(-sideBraceOffset, sideBraceCenterY, 0.0f),
                new Vector3(BeamHalfSize, sideBraceHalfHeight, BeamHalfSize),
                shape => {
                    shape.Swatch = TrackRailSwatch.Main;
                    shape.Specular = WoodSpecular;
                });

            // right vertical beam
            part.AppendBracePart.Box(
                new Vector3(sideBraceOffset, sideBraceCenterY, 0.0f),
                new Vector3(BeamHalfSize, sideBraceHalfHeight, BeamHalfSize),
                shape => {
                    shape.Swatch = TrackRailSwatch.Main;
                    shape.Specular = WoodSpecular;
                });
        }

        return part;
    }

    //private static IEnumerable<TrackRailProfileShape> CreateWoodProfile(float centerX, float centerY, float halfWidth, float halfHeight, TrackRailSwatch swatch)
    //{
    //    return CreateBox(centerX, centerY, halfWidth, halfHeight, shape => {
    //        shape.Swatch = swatch;
    //        shape.Specular = WoodSpecular;
    //    });
    //}
}
