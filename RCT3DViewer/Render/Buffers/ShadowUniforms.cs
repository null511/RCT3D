using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RCT3D.Render.Buffers;

[InlineArray((int)ShadowRenderTargets.CascadeCount)]
public struct ShadowViewProjections
{
    private Matrix4x4 _element;
}

[StructLayout(LayoutKind.Sequential)]
public struct ShadowUniforms
{
    public ShadowViewProjections ShadowViewProjection;
    public Vector4 CascadeSplits;
}
