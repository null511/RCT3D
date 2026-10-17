namespace RCT3DViewer.Shaders
{
    internal static class ShaderLoader
    {
        public static byte[]? LoadSpirv(string path)
        {
            if (File.Exists(path)) return File.ReadAllBytes(path);

            // Fallback: try output directory relative path (for build-time compiled shaders)
            var alt = Path.Combine(AppContext.BaseDirectory, Path.GetFileName(path));
            if (File.Exists(alt)) return File.ReadAllBytes(alt);

            return null;
        }
    }
}
