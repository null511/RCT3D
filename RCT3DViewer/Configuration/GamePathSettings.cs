namespace RCT3D.Configuration;

internal sealed record GamePathSettings
{
    public string? Rct1Path { get; init; }
    public string? Rct2Path { get; init; }
    public string? OpenRCT2Path { get; init; }
}
