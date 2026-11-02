using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Buffers;

[StructLayout(LayoutKind.Sequential)]
public struct ShadowUniforms
{
    public Matrix4x4 ShadowViewProjection0;
    public Matrix4x4 ShadowViewProjection1;
    public Matrix4x4 ShadowViewProjection2;
    public Matrix4x4 ShadowViewProjection3;
    public Vector4 CascadeSplits;
}
