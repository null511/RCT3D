using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Buffers;

[StructLayout(LayoutKind.Sequential)]
public struct CameraUniforms
{
    public Vector4 CameraPosition;
    public Matrix4x4 ViewProjection;
    public Matrix4x4 ProjectionInverse;
    public Matrix4x4 ViewToWorld;
}
