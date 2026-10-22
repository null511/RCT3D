using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Buffers;

[StructLayout(LayoutKind.Sequential)]
public struct TemporalUniforms
{
    public Vector4 CameraPositionLast;
    public Matrix4x4 ViewProjectionLast;
}
