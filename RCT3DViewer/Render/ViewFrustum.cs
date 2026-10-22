using System.Numerics;

namespace RCT3D.Render;

internal readonly struct ViewFrustum
{
    private readonly FrustumPlane _left;
    private readonly FrustumPlane _right;
    private readonly FrustumPlane _bottom;
    private readonly FrustumPlane _top;
    private readonly FrustumPlane _near;
    private readonly FrustumPlane _far;

    private ViewFrustum(
        FrustumPlane left,
        FrustumPlane right,
        FrustumPlane bottom,
        FrustumPlane top,
        FrustumPlane near,
        FrustumPlane far)
    {
        _left = left;
        _right = right;
        _bottom = bottom;
        _top = top;
        _near = near;
        _far = far;
    }

    public static ViewFrustum FromViewProjection(Matrix4x4 matrix)
    {
        // The shader transforms row vectors: clip = position * matrix.
        // Vulkan clip space uses -w <= x,y <= w and 0 <= z <= w.
        return new ViewFrustum(
            CreatePlane(new Vector4(matrix.M11 + matrix.M14, matrix.M21 + matrix.M24,
                matrix.M31 + matrix.M34, matrix.M41 + matrix.M44)),
            CreatePlane(new Vector4(matrix.M14 - matrix.M11, matrix.M24 - matrix.M21,
                matrix.M34 - matrix.M31, matrix.M44 - matrix.M41)),
            CreatePlane(new Vector4(matrix.M12 + matrix.M14, matrix.M22 + matrix.M24,
                matrix.M32 + matrix.M34, matrix.M42 + matrix.M44)),
            CreatePlane(new Vector4(matrix.M14 - matrix.M12, matrix.M24 - matrix.M22,
                matrix.M34 - matrix.M32, matrix.M44 - matrix.M42)),
            CreatePlane(new Vector4(matrix.M13, matrix.M23, matrix.M33, matrix.M43)),
            CreatePlane(new Vector4(matrix.M14 - matrix.M13, matrix.M24 - matrix.M23,
                matrix.M34 - matrix.M33, matrix.M44 - matrix.M43)));
    }

    public bool Intersects(AxisAlignedBounds bounds) =>
        _left.Intersects(bounds) &&
        _right.Intersects(bounds) &&
        _bottom.Intersects(bounds) &&
        _top.Intersects(bounds) &&
        _near.Intersects(bounds) &&
        _far.Intersects(bounds);

    private static FrustumPlane CreatePlane(Vector4 coefficients)
    {
        var normal = new Vector3(coefficients.X, coefficients.Y, coefficients.Z);
        var length = normal.Length();
        if (!float.IsFinite(length) || length <= float.Epsilon || !float.IsFinite(coefficients.W))
            throw new ArgumentException("View-projection matrix produced an invalid frustum plane.", nameof(coefficients));

        return new FrustumPlane(normal / length, coefficients.W / length);
    }

    private readonly record struct FrustumPlane(Vector3 Normal, float Distance)
    {
        public bool Intersects(AxisAlignedBounds bounds)
        {
            var positiveVertex = new Vector3(
                Normal.X >= 0.0f ? bounds.Max.X : bounds.Min.X,
                Normal.Y >= 0.0f ? bounds.Max.Y : bounds.Min.Y,
                Normal.Z >= 0.0f ? bounds.Max.Z : bounds.Min.Z);
            return Vector3.Dot(Normal, positiveVertex) + Distance >= 0.0f;
        }
    }
}
