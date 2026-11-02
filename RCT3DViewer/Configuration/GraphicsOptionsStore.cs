using System.Text.Json;

namespace RCT3D.Configuration;

internal sealed class GraphicsOptionsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public GraphicsOptionsStore()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
            throw new InvalidOperationException("The per-user application data directory is unavailable.");

        _settingsPath = Path.Combine(localApplicationData, "RCT3DViewer", "graphics-options.json");
    }

    public GraphicsOptionsSettings Load()
    {
        try {
            if (!File.Exists(_settingsPath))
                return GraphicsOptionsSettings.Defaults;

            var settings = JsonSerializer.Deserialize<GraphicsOptionsSettings>(File.ReadAllText(_settingsPath));
            return settings is not null && IsValid(settings) ? settings : GraphicsOptionsSettings.Defaults;
        }
        catch (JsonException) {
            return GraphicsOptionsSettings.Defaults;
        }
        catch (IOException) {
            return GraphicsOptionsSettings.Defaults;
        }
        catch (UnauthorizedAccessException) {
            return GraphicsOptionsSettings.Defaults;
        }
    }

    public void Save(GraphicsOptionsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsValid(settings))
            throw new ArgumentOutOfRangeException(nameof(settings));

        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"graphics-options-{Guid.NewGuid():N}.tmp");
        try {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                JsonSerializer.Serialize(stream, settings, SerializerOptions);
                stream.Flush(true);
            }

            File.Move(temporaryPath, _settingsPath, true);
        }
        finally {
            try {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException) {
            }
            catch (UnauthorizedAccessException) {
            }
        }
    }

    private static bool IsValid(GraphicsOptionsSettings settings) =>
        settings.DebugView is >= 0 and <= 3
        && float.IsFinite(settings.FieldOfView) && settings.FieldOfView is >= 30.0f and <= 120.0f
        && float.IsFinite(settings.Sharpening) && settings.Sharpening is >= 0.0f and <= 1.0f
        && float.IsFinite(settings.MipBias) && settings.MipBias is >= -2.0f and <= 0.0f
        && settings.ShadowResolution is 512 or 1024 or 2048 or 4096
        && float.IsFinite(settings.ShadowRadius) && settings.ShadowRadius is >= 16.0f and <= 512.0f;
}
