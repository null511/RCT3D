using System.Runtime.InteropServices;

namespace RCT3DViewer.Interop
{
    internal static class NativeLoader
    {
        public static string LastError { get; private set; } = "Native library was not found in the application directory or system search paths.";

        public static bool TryLoadNativeLibrary(string baseName, out IntPtr handle)
        {
            handle = IntPtr.Zero;
            LastError = $"{baseName} was not found in the application directory or system search paths.";
            var candidates = GetPlatformNames(baseName);

            // Search application base directory first, then rely on system loader.
            var appDir = AppContext.BaseDirectory;
            foreach (var name in candidates)
            {
                var path = Path.Combine(appDir, name);
                if (File.Exists(path))
                {
                    if (NativeLibrary.TryLoad(path, out handle)) return true;
                }
            }

            // Try names directly (system search paths)
            foreach (var name in candidates)
            {
                if (NativeLibrary.TryLoad(name, out handle)) return true;
            }

            return false;
        }

        public static bool TryLoadNativeLibrary(string path, out IntPtr handle, out string error)
        {
            handle = IntPtr.Zero;
            if (!File.Exists(path))
            {
                error = $"Native library does not exist: {path}";
                return false;
            }

            if (NativeLibrary.TryLoad(path, out handle))
            {
                error = string.Empty;
                return true;
            }

            error = $"Unable to load native library at {path}; a dependent DLL may be missing or have the wrong architecture.";
            return false;
        }

        private static string[] GetPlatformNames(string baseName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new[] { baseName + ".dll", baseName + ".lib" };
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new[] { "lib" + baseName + ".so", baseName + ".so" };
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new[] { "lib" + baseName + ".dylib", baseName + ".dylib" };
            }

            return new[] { baseName };
        }
    }
}
