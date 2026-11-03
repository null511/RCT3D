using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Terrain;

[StructLayout(LayoutKind.Sequential)]
internal struct WaterVertex(Vector3 position, Vector3 normal)
{
    public Vector3 Position = position;
    public Vector3 Normal = normal;
}
