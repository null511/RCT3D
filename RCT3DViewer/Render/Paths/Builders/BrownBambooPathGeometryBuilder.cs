using RCT3D.Render.Paths.Meshing;
using RCT3D.Render.Rides.Tracks;

namespace RCT3D.Render.Paths.Builders;

internal sealed class BrownBambooPathGeometryBuilder(PathMaterialLibrary materials) : BambooPathGeometryBuilder(materials, ObjectId, new TrackRgba8(145, 99, 51, 255))
{
    public const string ObjectId = "rct2.footpath_railings.bamboo_brown";
}
