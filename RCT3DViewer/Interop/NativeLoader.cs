using System.Runtime.InteropServices;

namespace RCT3D.Interop
{
    internal static class NativeLoader
    {
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

            error = $"Unable to load native library at {path}; a dependent native library may be missing or have the wrong architecture.";
            return false;
        }
    }
}
