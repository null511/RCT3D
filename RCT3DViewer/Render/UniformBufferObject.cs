using System.Numerics;
using System.Runtime.InteropServices;

namespace RCT3DViewer.Render;

[StructLayout(LayoutKind.Sequential)]
public struct UniformBufferObject
{
    public Matrix4x4 ViewProjection;
}
