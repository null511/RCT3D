using Microsoft.Extensions.Configuration;

namespace RCT3D.Configuration;

internal sealed class GamePathConfiguration
{
    private readonly GamePathSettingsStore store;
    private readonly Dictionary<string, string> overrides;

    public GamePathSettings Saved { get; private set; }
    public string? LoadError { get; }


    public GamePathConfiguration(
        GamePathSettingsStore store,
        IConfiguration configuration)
    {
        this.store = store;

        overrides = configuration.AsEnumerable()
            .Where(pair => pair.Key.Equals("RCT1_PATH", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("RCT2_PATH", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("OPENRCT2_DATA", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        Saved = store.Load(out var error);
        LoadError = error;
    }

    public GamePathSettings Effective => new() {
        Rct1Path = Resolve("RCT1_PATH", Saved.Rct1Path),
        Rct2Path = Resolve("RCT2_PATH", Saved.Rct2Path),
        OpenRCT2Path = Resolve("OPENRCT2_DATA", Saved.OpenRCT2Path)
    };

    public string GetSource(string key)
    {
        var saved = key switch {
            "RCT1_PATH" => Saved.Rct1Path,
            "RCT2_PATH" => Saved.Rct2Path,
            "OPENRCT2_DATA" => Saved.OpenRCT2Path,
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };

        return overrides.ContainsKey(key) ? "Environment" : saved is not null ? "Saved settings" : "Not configured";
    }

    public void Save(GamePathSettings settings)
    {
        store.Save(settings);
        Saved = settings;
    }

    private string? Resolve(string key, string? saved) => overrides.TryGetValue(key, out var value) ? value : saved;
}
