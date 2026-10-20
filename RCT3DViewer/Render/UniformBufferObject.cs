using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Maths;

namespace RCT3DViewer.Render;

[StructLayout(LayoutKind.Sequential)]
public struct CameraUniforms
{
    public Vector4 CameraPosition;
    public Matrix4x4 ViewProjection;
    public Matrix4x4 ViewProjectionInverse;
}

[StructLayout(LayoutKind.Sequential)]
public struct TemporalUniforms
{
    public Vector4 CameraPositionLast;
    public Matrix4x4 ViewProjectionLast;
}

[StructLayout(LayoutKind.Sequential)]
public struct SceneUniforms
{
    public Vector2D<uint> RenderDimensions;
    public Vector2 TaaJitter;
    public Vector4 SunDirection;
    public Vector4 Lighting;
    public uint FrameCounter;
    public Vector3 Padding;
}
