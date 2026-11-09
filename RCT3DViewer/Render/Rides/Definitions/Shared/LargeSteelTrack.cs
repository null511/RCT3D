using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions.Shared;

internal static class LargeSteelTrack
{
    private static readonly TrackRailSpecularData railSpecular = new(100, 0);

    private const float BeamHalfWidth = 0.38f;
    private const float BeamHalfHeight = 0.42f;
    private const float BeamCenterY = -0.70f;

    private const float RailSeparation = 0.67f;
    private const float RailCenterY = 0.0f;
    private const float RailRadius = 0.07f;

    private const float BraceHalfThickness = 0.03f;


    public static TrackPart Build(RCTTrackElementData track)
    {
        var part = new TrackPart {
            BraceInterval = 0.8f,
        };

        // left
        part.AppendRailPart.Circle(-RailSeparation, RailCenterY, RailRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        // right
        part.AppendRailPart.Circle(RailSeparation, RailCenterY, RailRadius, 5, shape => {
            shape.Swatch = TrackRailSwatch.Alternate;
            shape.Specular = railSpecular;
        });

        // center
        part.AppendRailPart.Box(0f, BeamCenterY, BeamHalfWidth, BeamHalfHeight, shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = railSpecular;
        });

        // brace
        var builder = new BraceMeshBuilder();

        AppendBraceFace(builder, false);
        AppendBraceFace(builder, true);
        AppendBraceEdges(builder);

        part.BraceParts.Add(builder.Build(shape => {
            shape.Swatch = TrackRailSwatch.Main;
            shape.Specular = railSpecular;
        }));

        return part;
    }

    private static void AppendBraceFace(BraceMeshBuilder builder, bool isBackFace)
    {
        var z = isBackFace ? BraceHalfThickness : -BraceHalfThickness;
        var beamTop = BeamCenterY + BeamHalfHeight;
        var splitY = beamTop + 0.1f;

        var vertices = new TrackBraceVertex[7];
        var indices = new uint[15];

        vertices[0] = V(new Vector3(    0f, splitY, z));
        vertices[1] = V(new Vector3(-BeamHalfWidth, beamTop, z));
        vertices[2] = V(new Vector3( BeamHalfWidth, beamTop, z));

        vertices[3] = V(new Vector3( RailSeparation, -0.08f, z));
        vertices[4] = V(new Vector3( RailSeparation,  0.00f, z));

        vertices[5] = V(new Vector3(-RailSeparation,  0.00f, z));
        vertices[6] = V(new Vector3(-RailSeparation, -0.08f, z));

        I( 0, 0, 1, 2);
        I( 3, 2, 3, 0);
        I( 6, 0, 3, 4);
        I( 9, 1, 0, 6);
        I(12, 6, 0, 5);

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
        var beamTop = BeamCenterY + BeamHalfHeight;
        var splitY = beamTop + 0.1f;

        var p1_1 = new Vector3(    0f, splitY, -BraceHalfThickness);
        var p1_2 = new Vector3(    0f, splitY,  BraceHalfThickness);

        var p5_1 = new Vector3( RailSeparation,  0.00f, -BraceHalfThickness);
        var p5_2 = new Vector3( RailSeparation,  0.00f,  BraceHalfThickness);

        var p6_1 = new Vector3(-RailSeparation,  0.00f, -BraceHalfThickness);
        var p6_2 = new Vector3(-RailSeparation,  0.00f,  BraceHalfThickness);

        // top right
        builder.AppendQuad(p1_2, p1_1, p5_1, p5_2);

        // top left
        builder.AppendQuad(p1_1, p1_2, p6_2, p6_1);

        // bottom left
        var p2_1 = new Vector3(-BeamHalfWidth, beamTop, -BraceHalfThickness);
        var p2_2 = new Vector3(-BeamHalfWidth, beamTop,  BraceHalfThickness);

        var p7_1 = new Vector3(-RailSeparation, -0.08f, -BraceHalfThickness);
        var p7_2 = new Vector3(-RailSeparation, -0.08f,  BraceHalfThickness);

        builder.AppendQuad(p2_2, p2_1, p7_1, p7_2);

        // bottom right
        var p3_1 = new Vector3( BeamHalfWidth, beamTop, -BraceHalfThickness);
        var p3_2 = new Vector3( BeamHalfWidth, beamTop,  BraceHalfThickness);

        var p4_1 = new Vector3( RailSeparation, -0.08f, -BraceHalfThickness);
        var p4_2 = new Vector3( RailSeparation, -0.08f,  BraceHalfThickness);

        builder.AppendQuad(p3_1, p3_2, p4_2, p4_1);
    }
}
