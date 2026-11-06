using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class LoopingRollerCoaster : RideDefinition
{
    private const float RailRadius = 0.07f;
    private const float RailSeparation = 0.42f;
    private const float RailCenterY = 0.28f;

    private const float BraceTopY = RailCenterY + 0.04f;
    private const float BraceBottomY = RailCenterY - 0.04f;
    private const float BraceCenterHeight = 0.18f;

    private const float BraceHalfThickness = 0.03f;

    private static readonly TrackRailSpecularData railSpecular = new(100, 0);


    public LoopingRollerCoaster() : base(RCTRideType.LoopingRollerCoaster)
    {
        StationBuilder = new DefaultTrackStationBuilder();
        SmoothMovement = true;
        CarVerticalOffset = 0.85f;

        SupportStyle = RideSupportStyle.MetalTubes;
        SupportTrackOffset = 0.20f;
    }

    protected override TrackPart GetTrackShape(RCTTrackElementData track)
    {
        var part = new TrackPart {
            BraceInterval = 0.8f,
            RailProfiles = [
                // left
                new TrackRailProfileShape(CreateCircle(-RailSeparation, RailCenterY, RailRadius, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                // right
                new TrackRailProfileShape(CreateCircle( RailSeparation, RailCenterY, RailRadius, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                // center
                new TrackRailProfileShape(CreateCircle(0.00f, 0.20f, 0.14f, 6), true) {
                    Swatch = TrackRailSwatch.Main,
                    Specular = railSpecular,
                },
            ],
        };

        var builder = new BraceMeshBuilder();

        AppendBraceFace(builder, false);
        AppendBraceFace(builder, true);
        AppendBraceEdges(builder);

        part.BraceParts.Add(builder.Build(p => {
            p.Swatch = TrackRailSwatch.Main;
            p.Specular = railSpecular;
        }));

        if (track.HasChainLift != 0) {
            part.RailProfiles.AddRange(CreateBox(0f, RailCenterY, RailSeparation - 0.08f, RailRadius, p => {
                p.Swatch = TrackRailSwatch.Main;
                p.Specular = railSpecular;
            }));
        }

        return part;
    }

    private static void AppendBraceFace(BraceMeshBuilder builder, bool isBackFace)
    {
        var z = isBackFace ? BraceHalfThickness : -BraceHalfThickness;
        var splitY = RailCenterY - BraceCenterHeight;

        var vertices = new TrackBraceVertex[5];
        var indices = new uint[9];

        vertices[0] = V(new Vector3(             0f,      splitY, z));
        vertices[1] = V(new Vector3( RailSeparation, BraceBottomY, z));
        vertices[2] = V(new Vector3(-RailSeparation, BraceBottomY, z));

        vertices[3] = V(new Vector3( RailSeparation, BraceTopY, z));
        vertices[4] = V(new Vector3(-RailSeparation, BraceTopY, z));

        I(0, 0, 1, 2);
        I(3, 2, 1, 4);
        I(6, 1, 3, 4);

        builder.Append(vertices, indices);
        return;

        void I(int index, uint v1, uint v2, uint v3) {
            if (isBackFace) {
                indices[index+2] = v1;
                indices[index+1] = v2;
                indices[index  ] = v3;
            }
            else {
                indices[index  ] = v1;
                indices[index+1] = v2;
                indices[index+2] = v3;
            }
        }

        TrackBraceVertex V(Vector3 position) {
            return new TrackBraceVertex {
                Position = position,
                Normal = isBackFace ? Vector3.UnitZ : -Vector3.UnitZ,
            };
        }
    }

    private static void AppendBraceEdges(BraceMeshBuilder builder)
    {
        var p4_1 = new Vector3( RailSeparation, BraceTopY, -BraceHalfThickness);
        var p4_2 = new Vector3( RailSeparation, BraceTopY,  BraceHalfThickness);

        var p5_1 = new Vector3(-RailSeparation, BraceTopY, -BraceHalfThickness);
        var p5_2 = new Vector3(-RailSeparation, BraceTopY,  BraceHalfThickness);

        // top
        builder.AppendQuad(p4_1, p4_2, p5_2, p5_1);

        var splitY = RailCenterY - BraceCenterHeight;

        var p1_1 = new Vector3(0f, splitY, -BraceHalfThickness);
        var p1_2 = new Vector3(0f, splitY,  BraceHalfThickness);

        var p2_1 = new Vector3( RailSeparation, BraceBottomY, -BraceHalfThickness);
        var p2_2 = new Vector3( RailSeparation, BraceBottomY,  BraceHalfThickness);

        var p3_1 = new Vector3(-RailSeparation, BraceBottomY, -BraceHalfThickness);
        var p3_2 = new Vector3(-RailSeparation, BraceBottomY,  BraceHalfThickness);

        // bottom left
        builder.AppendQuad(p1_2, p1_1, p3_1, p3_2);

        // bottom right
        builder.AppendQuad(p1_1, p1_2, p2_2, p2_1);
    }
}
