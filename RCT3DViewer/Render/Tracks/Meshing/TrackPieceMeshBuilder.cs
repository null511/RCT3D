using System.Numerics;
using RCT3D.Interop;

namespace RCT3D.Render.Tracks.Meshing;

internal interface ITrackPieceMeshBuilder
{
    RCTTrackElementType PieceType { get; }
    void Add(in RCTTrackElementData track, TrackMeshGeometry geometry);
    void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry);
}

internal abstract class TrackPieceMeshBuilder(RCTTrackElementType pieceType) : ITrackPieceMeshBuilder
{
    public RCTTrackElementType PieceType { get; } = pieceType;

    public abstract void Add(in RCTTrackElementData track, TrackMeshGeometry geometry);

    public virtual void AddPiece(ReadOnlySpan<RCTTrackElementData> tracks, TrackMeshGeometry geometry)
    {
        foreach (ref readonly var track in tracks)
            Add(in track, geometry);
    }
}

internal readonly record struct TrackPartProfile(float Width, float Thickness);

internal static class TrackMeshPartGenerator
{
    public static void AddSweptPrism(
        TrackMeshGeometry geometry,
        ReadOnlySpan<Vector3> centerline,
        TrackPartProfile profile,
        byte paletteIndex)
    {
        if (centerline.Length < 2 || profile.Width <= 0.0f || profile.Thickness <= 0.0f)
            return;

        for (var i = 0; i < centerline.Length - 1; i++)
            AddPrismSegment(geometry, centerline[i], centerline[i + 1], profile, paletteIndex);
    }

    private static void AddPrismSegment(
        TrackMeshGeometry geometry,
        Vector3 start,
        Vector3 end,
        TrackPartProfile profile,
        byte paletteIndex)
    {
        var forward = end - start;
        if (forward.LengthSquared() <= float.Epsilon)
            return;

        forward = Vector3.Normalize(forward);
        var side = Vector3.Cross(forward, Vector3.UnitY);
        if (side.LengthSquared() <= float.Epsilon)
            side = Vector3.UnitX;
        else
            side = Vector3.Normalize(side);
        var up = Vector3.Normalize(Vector3.Cross(side, forward));
        var halfWidth = side * (profile.Width * 0.5f);
        var halfThickness = up * (profile.Thickness * 0.5f);

        var a = start - halfWidth - halfThickness;
        var b = start + halfWidth - halfThickness;
        var c = start + halfWidth + halfThickness;
        var d = start - halfWidth + halfThickness;
        var e = end - halfWidth - halfThickness;
        var f = end + halfWidth - halfThickness;
        var g = end + halfWidth + halfThickness;
        var h = end - halfWidth + halfThickness;

        geometry.AddQuad(a, b, f, e, paletteIndex, -up);
        geometry.AddQuad(d, h, g, c, paletteIndex, up);
        geometry.AddQuad(a, e, h, d, paletteIndex, -side);
        geometry.AddQuad(b, c, g, f, paletteIndex, side);
        geometry.AddQuad(a, d, c, b, paletteIndex, -forward);
        geometry.AddQuad(e, f, g, h, paletteIndex, forward);
    }
}
