using RCT3D.Render.Rides.Supports.Builders;

namespace RCT3D.Render.Rides.Supports;

internal enum RideSupportStyle
{
    None,
    MetalBoxed,
    MetalFork,
    MetalTubes,
    MetalTubesInverted,
    WoodenTruss,
}

internal static class RideSupportMeshBuilders
{
    public static IRideSupportMeshBuilder? Resolve(RideSupportStyle style) => style switch {
        RideSupportStyle.MetalBoxed => new MetalBoxedSupportMeshBuilder(),
        RideSupportStyle.MetalFork => new MetalForkSupportMeshBuilder(),
        RideSupportStyle.MetalTubes => new MetalTubesSupportMeshBuilder(),
        RideSupportStyle.WoodenTruss => new WoodenTrussSupportMeshBuilder(),
        _ => null,
    };
}