namespace RCT3D.Render.Tracks.Meshing;

internal enum RideSupportStyle
{
    None,
    MetalBoxed,
    MetalFork,
    MetalTubes,
    WoodenTruss,
}

internal static class RideSupportMeshBuilders
{
    public static IRideSupportMeshBuilder? Resolve(RideSupportStyle style) => style switch {
        RideSupportStyle.MetalBoxed => new MetalBoxedSupportMeshBuilder(),
        _ => null,
    };
}