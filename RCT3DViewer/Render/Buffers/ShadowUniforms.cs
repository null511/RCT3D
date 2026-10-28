using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Buffers;

[StructLayout(LayoutKind.Sequential)]
public struct ShadowUniforms
{
    public Matrix4x4 ShadowViewProjection;
    public float DistortionFactor;
    public Vector3 Padding;
}
