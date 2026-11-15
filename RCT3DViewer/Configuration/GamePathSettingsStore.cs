using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RCT3D.Configuration;

internal sealed class GamePathSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    private readonly string? _settingsPath;
    private readonly ILogger<GamePathSettingsStore> logger;


    public GamePathSettingsStore(ILogger<GamePathSettingsStore> logger)
    {
        this.logger = logger;

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrWhiteSpace(localApplicationData))
            _settingsPath = Path.Combine(localApplicationData, "RCT3DViewer", "game-paths.json");
    }

    public GamePathSettings Load(out string? error)
    {
        error = null;
        try {
            EnsureStorageAvailable();
            using var stream = new FileStream(_settingsPath!, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize<GamePathSettings>(stream, SerializerOptions)
                ?? throw new JsonException("The saved game path settings must be a JSON object, not null.");
        }
        catch (FileNotFoundException) {
            return new GamePathSettings();
        }
        catch (DirectoryNotFoundException) {
            return new GamePathSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) {
            error = $"Could not read saved game paths ({_settingsPath ?? "per-user application data unavailable"}): {ex.Message}";
            logger.LogError(ex, "{ConfigurationError}", error);
            return new GamePathSettings();
        }
    }

    public GamePathSettings Load()
    {
        var settings = Load(out var error);

        if (error is not null)
            throw new InvalidOperationException(error);

        return settings;
    }

    // Callers commit their edited settings only after this method returns successfully.
    public void Save(GamePathSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var validationError = Validate(settings);
        if (validationError is not null) {
            logger.LogError("Could not save game paths: {ValidationError}", validationError);
            throw new ArgumentException(validationError, nameof(settings));
        }

        string? temporaryPath = null;
        try {
            EnsureStorageAvailable();
            var directory = Path.GetDirectoryName(_settingsPath!)!;
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(directory, $"game-paths-{Guid.NewGuid():N}.tmp");
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                JsonSerializer.Serialize(stream, settings, SerializerOptions);
                stream.Flush(true);
            }

            File.Move(temporaryPath, _settingsPath!, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) {
            logger.LogError(ex, "Could not save game paths to {SettingsPath}", _settingsPath);
            throw;
        }
        finally {
            if (temporaryPath is not null) {
                try {
                    File.Delete(temporaryPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                    logger.LogWarning(ex, "Could not remove game path settings staging file {TemporaryPath}", temporaryPath);
                }
            }
        }
    }

    public static string? Validate(GamePathSettings settings, bool requireAll = false)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = new List<string>();
        ValidateDirectory("RCT1_PATH", settings.Rct1Path);
        ValidateDirectory("RCT2_PATH", settings.Rct2Path);
        ValidateDirectory("OPENRCT2_DATA", settings.OpenRCT2Path);
        return errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);

        void ValidateDirectory(string key, string? value) {
            if (string.IsNullOrWhiteSpace(value)) {
                if (requireAll)
                    errors.Add($"{key}: an installation directory is required.");
                return;
            }

            try {
                var path = ResolveDirectoryPath(value);
                if (!Directory.Exists(path))
                    errors.Add($"{key}: directory does not exist or is inaccessible: {path}");
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException) {
                errors.Add($"{key}: invalid directory: {ex.Message}");
            }
        }
    }

    public static string ResolveDirectoryPath(string path) => Path.GetFullPath(path, AppContext.BaseDirectory);

    private void EnsureStorageAvailable()
    {
        if (_settingsPath is null)
            throw new InvalidOperationException("The per-user application data directory is unavailable.");
    }
}
