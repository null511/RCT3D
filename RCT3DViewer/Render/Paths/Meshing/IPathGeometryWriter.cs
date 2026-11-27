using System.Numerics;

namespace RCT3D.Render.Paths.Meshing;

internal interface IPathGeometryWriter
{
    uint AddIndexedVertex(Vector3 position, Vector3 normal, Vector2 uv, uint material);

    void AddIndexedTriangle(uint a, uint b, uint c);

    void AddTriangle(
        Vector3 a, Vector2 uvA, Vector3 b, Vector2 uvB, Vector3 c, Vector2 uvC,
        uint material, Vector3? expectedNormal = null);

    void AddPrismFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 prismCenter, uint material);
}
