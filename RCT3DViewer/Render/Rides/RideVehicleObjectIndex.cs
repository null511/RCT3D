using System.Globalization;
using System.IO.Compression;
using System.Text.Json;

namespace RCT3D.Render.Rides;

internal static class RideVehicleObjectIndex
{
    public static string NormalizeName(string name) =>
        string.Concat(name.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    public static Dictionary<string, HashSet<string>> Load(string objectDirectory)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(objectDirectory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)) {
            var extension = Path.GetExtension(file);
            if (!extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".parkobj", StringComparison.OrdinalIgnoreCase))
                continue;

            try {
                if (extension.Equals(".parkobj", StringComparison.OrdinalIgnoreCase)) {
                    using var archive = ZipFile.OpenRead(file);
                    var entry = archive.GetEntry("object.json")
                        ?? throw new InvalidDataException("Archive does not contain object.json.");
                    using var stream = entry.Open();
                    ReadObject(stream);
                }
                else {
                    using var stream = File.OpenRead(file);
                    ReadObject(stream);
                }
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException
                or InvalidOperationException or FormatException or OverflowException) {
                throw new InvalidDataException($"Invalid OpenRCT2 object metadata '{file}': {exception.Message}", exception);
            }
        }

        return result;

        void ReadObject(Stream stream) {
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (!root.TryGetProperty("objectType", out var objectType) || objectType.GetString() != "ride")
                return;

            if (!root.TryGetProperty("strings", out var strings)
                || !strings.TryGetProperty("name", out var names))
                return;

            string? name = null;
            if (names.TryGetProperty("en-GB", out var britishName))
                name = britishName.GetString();
            if (string.IsNullOrWhiteSpace(name) && names.TryGetProperty("en-US", out var americanName))
                name = americanName.GetString();
            if (string.IsNullOrWhiteSpace(name))
                return;

            var key = NormalizeName(name);
            if (key.Length == 0)
                return;

            if (!result.TryGetValue(key, out var identifiers)) {
                identifiers = new HashSet<string>(StringComparer.Ordinal);
                result.Add(key, identifiers);
            }

            if (root.TryGetProperty("id", out var id) && !string.IsNullOrWhiteSpace(id.GetString()))
                identifiers.Add(id.GetString()!);

            if (root.TryGetProperty("originalId", out var originalId)
                && !string.IsNullOrWhiteSpace(originalId.GetString()))
                identifiers.Add(FormatLegacyIdentifier(originalId.GetString()!));
        }
    }

    private static string FormatLegacyIdentifier(string identifier)
    {
        var parts = identifier.Split('|');
        if (parts.Length != 3 || parts[0].Length != 8 || parts[1].Length != 8 || parts[2].Length != 8)
            throw new InvalidDataException($"Invalid originalId '{identifier}'.");

        var flags = uint.Parse(parts[0].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var checksum = uint.Parse(parts[2].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return $"{flags:X8}|{parts[1]}|{checksum.ToString("X", CultureInfo.InvariantCulture).PadLeft(8)}";
    }
}
