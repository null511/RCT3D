using RCT3D.Render.Paths.Meshing;

namespace RCT3D.Render.Paths.Builders;

internal sealed class BlackBambooPathGeometryBuilder(PathMaterialLibrary materials) : BambooPathGeometryBuilder(materials, ObjectId)
{
    public const string ObjectId = "rct2.footpath_railings.bamboo_black";
}
