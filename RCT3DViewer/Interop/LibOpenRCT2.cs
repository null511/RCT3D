using System.Runtime.InteropServices;
using System.Text;

namespace RCT3DViewer.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RCTTileSurfaceData
{
    public byte BaseZ;
    public byte SlopeFlags;
    public ushort GroundType;
    public byte NorthCornerHeight;
    public byte EastCornerHeight;
    public byte SouthCornerHeight;
    public byte WestCornerHeight;
    public byte MapColour;
    public byte Reserved;
}

public sealed unsafe class LibOpenRCT2 : IDisposable
{
    private const string LibName = "libopenrct2";
    private const uint ExpectedAbiVersion = 3;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint GetAbiVersionDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitializeDelegate(byte* openRCT2Path, byte* rct2Path, out IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int LoadParkDelegate(IntPtr context, byte* parkPath);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetMapDimensionsDelegate(IntPtr context, int* width, int* height);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CopySurfaceDataDelegate(IntPtr context, RCTTileSurfaceData* output, ulong capacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetLastErrorDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ShutdownDelegate(IntPtr context);

    private readonly IntPtr _nativeHandle;
    private readonly LoadParkDelegate _loadPark;
    private readonly GetMapDimensionsDelegate _getMapDimensions;
    private readonly CopySurfaceDataDelegate _copySurfaceData;
    private readonly GetLastErrorDelegate _getLastError;
    private readonly ShutdownDelegate _shutdown;
    private IntPtr _context;
    private bool _disposed;

    public bool IsAvailable => !_disposed && _context != IntPtr.Zero;

    public LibOpenRCT2(string libraryPath, string openRCT2Path, string rct2Path)
    {
        if (!NativeLoader.TryLoadNativeLibrary(libraryPath, out _nativeHandle, out var error))
            throw new InvalidOperationException(error);

        try
        {
            var getAbiVersion = Bind<GetAbiVersionDelegate>("rct3d_openrct2_get_abi_version");
            var initialize = Bind<InitializeDelegate>("rct3d_openrct2_initialize");
            _loadPark = Bind<LoadParkDelegate>("rct3d_openrct2_load_park");
            _getMapDimensions = Bind<GetMapDimensionsDelegate>("rct3d_openrct2_get_map_dimensions");
            _copySurfaceData = Bind<CopySurfaceDataDelegate>("rct3d_openrct2_copy_surface_data");
            _getLastError = Bind<GetLastErrorDelegate>("rct3d_openrct2_get_last_error");
            _shutdown = Bind<ShutdownDelegate>("rct3d_openrct2_shutdown");

            var version = getAbiVersion();
            if (version != ExpectedAbiVersion)
                throw new InvalidOperationException($"Unsupported OpenRCT2 adapter ABI version {version}; expected {ExpectedAbiVersion}.");

            var openRCT2PathUtf8 = Encoding.UTF8.GetBytes(openRCT2Path + '\0');
            var rct2PathUtf8 = Encoding.UTF8.GetBytes(rct2Path + '\0');
            fixed (byte* openRCT2PathPointer = openRCT2PathUtf8)
            fixed (byte* rct2PathPointer = rct2PathUtf8)
            {
                var status = initialize(openRCT2PathPointer, rct2PathPointer, out var context);
                ThrowIfFailed(status);
                _context = context;
            }
        }
        catch
        {
            NativeLibrary.Free(_nativeHandle);
            throw;
        }
    }

    public static string DefaultLibraryPath => Path.Combine(AppContext.BaseDirectory, LibName + ".dll");

    public void LoadPark(string parkPath)
    {
        var pathUtf8 = Encoding.UTF8.GetBytes(parkPath + '\0');
        fixed (byte* pathPointer = pathUtf8)
            ThrowIfFailed(_loadPark(_context, pathPointer));
    }

    public (int Width, int Height) GetDimensions()
    {
        int width = 0;
        int height = 0;
        ThrowIfFailed(_getMapDimensions(_context, &width, &height));
        return (width, height);
    }

    public void CopySurfaceData(Span<RCTTileSurfaceData> output)
    {
        fixed (RCTTileSurfaceData* pointer = output)
            ThrowIfFailed(_copySurfaceData(_context, pointer, (ulong)output.Length));
    }

    private T Bind<T>(string name) where T : Delegate
    {
        if (!NativeLibrary.TryGetExport(_nativeHandle, name, out var address))
            throw new EntryPointNotFoundException($"The loaded {LibName} does not export {name}.");
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private void ThrowIfFailed(int status)
    {
        if (status == 0)
            return;

        var errorPointer = _getLastError();
        var message = errorPointer == IntPtr.Zero ? "Unknown native error." : Marshal.PtrToStringUTF8(errorPointer);
        throw new InvalidOperationException($"OpenRCT2 adapter failed with status {status}: {message}");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_context != IntPtr.Zero)
            ThrowIfFailed(_shutdown(_context));

        _context = IntPtr.Zero;
        _disposed = true;
        NativeLibrary.Free(_nativeHandle);
    }
}
