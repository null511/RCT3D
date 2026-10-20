using Silk.NET.Maths;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3DViewer.Render.Buffers;

[StructLayout(LayoutKind.Sequential)]
public struct SceneUniforms
{
    public Vector2D<uint> RenderDimensions;
    public Vector2 TaaJitter;
    public Vector4 SunDirection;
    public Vector4 Lighting;
    public uint FrameCounter;
    public float TaaSharpen;
    public float MipBias;
    public float Padding;
}
