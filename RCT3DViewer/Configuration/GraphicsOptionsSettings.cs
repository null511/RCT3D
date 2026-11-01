namespace RCT3D.Configuration;

internal sealed record GraphicsOptionsSettings
{
    public bool TaaEnabled { get; init; } = false;
    public int DebugView { get; init; } = 0;
    public float FieldOfView { get; init; } = 45.0f;
    public float Sharpening { get; init; } = 0.5f;
    public float MipBias { get; init; } = -1.0f;
    public uint ShadowResolution { get; init; } = 2048;
    public float ShadowRadius { get; init; } = 128.0f;
    public float ShadowDistortionFactor { get; init; } = 0.65f;

    public GraphicsOptionsSettings()
    {
    }

    public static GraphicsOptionsSettings Defaults { get; } = new();
}
