using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Supports;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Render.Rides.Tracks.Builders;
using System.Numerics;

namespace RCT3D.Render.Rides.Definitions;

internal class VerticalDropRollerCoaster : RideDefinition
{
    private static readonly TrackRailSpecularData railSpecular = new(100, 0);

    private const float RailSeparation = 0.65f;


    public VerticalDropRollerCoaster() : base(RCTRideType.VerticalDropRollerCoaster)
    {
        SmoothMovement = true;
        CenterlineHeightOffset = 0.75f;
        CarVerticalOffset = 0.5f;

        SupportStyle = RideSupportStyle.MetalBoxed;

        StationBuilder = new DefaultTrackStationBuilder();
    }

    protected override TrackShape GetTrackShape(RCTTrackElementData track)
    {
        const float railCenterY = 0.0f;
        const float railRadius = 0.08f;

        var part = new TrackShape {
            BraceInterval = 0.8f,
            RailShapes = [
                // left
                new TrackRailProfileShape(CreateCircle(-RailSeparation, railCenterY, railRadius, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                // right
                new TrackRailProfileShape(CreateCircle( RailSeparation, railCenterY, railRadius, 5), true) {
                    Swatch = TrackRailSwatch.Alternate,
                    Specular = railSpecular,
                },
                // center
                .. CreateBox(0f, -0.50f, 0.24f, 0.20f, p => {
                    p.Swatch = TrackRailSwatch.Main;
                    p.Specular = railSpecular;
                })
            ],
        };

        var builder = new BraceMeshBuilder();

        var frontFace = BuildBraceFace(false);
        builder.Append(frontFace.vertices, frontFace.indices);

        var backFace = BuildBraceFace(true);
        builder.Append(backFace.vertices, backFace.indices);

        // TODO: exposed side faces

        part.BraceParts.Add(builder.Build(p => {
            p.Swatch = TrackRailSwatch.Main;
            p.Specular = railSpecular;
        }));

        return part;
    }

    private static (TrackBraceVertex[] vertices, uint[] indices) BuildBraceFace(bool isBackFace)
    {
        const float halfThickness = 0.03f;

        var z = isBackFace ? halfThickness : -halfThickness;

        var vertices = new TrackBraceVertex[7];
        var indices = new uint[15];

        vertices[0] = V(new Vector3(    0f, -0.20f, z));
        vertices[1] = V(new Vector3(-0.24f, -0.30f, z));
        vertices[2] = V(new Vector3( 0.24f, -0.30f, z));

        vertices[3] = V(new Vector3( RailSeparation, -0.08f, z));
        vertices[4] = V(new Vector3( RailSeparation,  0.00f, z));

        vertices[5] = V(new Vector3(-RailSeparation,  0.00f, z));
        vertices[6] = V(new Vector3(-RailSeparation, -0.08f, z));

        I( 0, 0, 1, 2);
        I( 3, 2, 3, 0);
        I( 6, 0, 3, 4);
        I( 9, 1, 0, 6);
        I(12, 6, 0, 5);

        return (vertices, indices);

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
                Normal = isBackFace ? Vector3.UnitZ : -Vector3.UnitZ, // TODO: this may be reversed
            };
        }
    }
}
