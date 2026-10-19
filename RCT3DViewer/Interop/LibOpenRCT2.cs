using System.Runtime.InteropServices;
using System.Text;

namespace RCT3DViewer.Interop;

public sealed unsafe class LibOpenRCT2 : IDisposable
{
    private const string LibName = "libopenrct2";
    private const uint ExpectedAbiVersion = 9;

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
    private delegate int GetPathCountDelegate(IntPtr context, out ulong count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CopyPathDataDelegate(IntPtr context, RCTTilePathData* output, ulong capacity);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetLastErrorDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ShutdownDelegate(IntPtr context);

    private readonly IntPtr _nativeHandle;
    private readonly LoadParkDelegate _loadPark;
    private readonly GetMapDimensionsDelegate _getMapDimensions;
    private readonly CopySurfaceDataDelegate _copySurfaceData;
    private readonly GetPathCountDelegate _getPathCount;
    private readonly CopyPathDataDelegate _copyPathData;
    private readonly GetLastErrorDelegate _getLastError;
    private readonly ShutdownDelegate _shutdown;
    private readonly string _libraryPath;
    private IntPtr _context;
    private bool _disposed;

    public static string DefaultLibraryPath => Path.Combine(AppContext.BaseDirectory, LibName + ".dll");
    //public bool IsAvailable => !_disposed && _context != IntPtr.Zero;


    public LibOpenRCT2(string libraryPath, string openRCT2Path, string rct2Path)
    {
        _libraryPath = Path.GetFullPath(libraryPath);
        if (!NativeLoader.TryLoadNativeLibrary(_libraryPath, out _nativeHandle, out var error))
            throw new InvalidOperationException(error);

        try {
            var getAbiVersion = Bind<GetAbiVersionDelegate>("rct3d_openrct2_get_abi_version");
            var version = getAbiVersion();
            if (version != ExpectedAbiVersion)
                throw new InvalidOperationException($"Unsupported OpenRCT2 adapter ABI version {version}; expected {ExpectedAbiVersion}. Loaded library: '{_libraryPath}'.");
            if (Marshal.SizeOf<RCTTileSurfaceData>() != 16)
                throw new InvalidOperationException("RCTTileSurfaceData must be 16 bytes to match the OpenRCT2 adapter ABI.");
            if (Marshal.SizeOf<RCTTilePathData>() != 28)
                throw new InvalidOperationException("RCTTilePathData must be 28 bytes to match the OpenRCT2 adapter ABI.");

            var initialize = Bind<InitializeDelegate>("rct3d_openrct2_initialize");
            _loadPark = Bind<LoadParkDelegate>("rct3d_openrct2_load_park");
            _getMapDimensions = Bind<GetMapDimensionsDelegate>("rct3d_openrct2_get_map_dimensions");
            _copySurfaceData = Bind<CopySurfaceDataDelegate>("rct3d_openrct2_copy_surface_data");
            _getPathCount = Bind<GetPathCountDelegate>("rct3d_openrct2_get_path_count");
            _copyPathData = Bind<CopyPathDataDelegate>("rct3d_openrct2_copy_path_data");
            _getLastError = Bind<GetLastErrorDelegate>("rct3d_openrct2_get_last_error");
            _shutdown = Bind<ShutdownDelegate>("rct3d_openrct2_shutdown");

            var openRCT2PathUtf8 = Encoding.UTF8.GetBytes(openRCT2Path + '\0');
            var rct2PathUtf8 = Encoding.UTF8.GetBytes(rct2Path + '\0');

            fixed (byte* openRCT2PathPointer = openRCT2PathUtf8)
            fixed (byte* rct2PathPointer = rct2PathUtf8) {
                var status = initialize(openRCT2PathPointer, rct2PathPointer, out var context);
                ThrowIfFailed(status);
                _context = context;
            }
        }
        catch {
            NativeLibrary.Free(_nativeHandle);
            throw;
        }
    }

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

    public ulong GetPathCount()
    {
        ThrowIfFailed(_getPathCount(_context, out var count));
        return count;
    }

    public void CopyPathData(Span<RCTTilePathData> output)
    {
        fixed (RCTTilePathData* pointer = output)
            ThrowIfFailed(_copyPathData(_context, pointer, (ulong)output.Length));
    }

    private T Bind<T>(string name) where T : Delegate
    {
        if (!NativeLibrary.TryGetExport(_nativeHandle, name, out var address))
            throw new EntryPointNotFoundException($"The loaded {LibName} at '{_libraryPath}' does not export {name}; rebuild or select the ABI {ExpectedAbiVersion} adapter.");

        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private void ThrowIfFailed(int status)
    {
        if (status == 0) return;

        var errorPointer = _getLastError();
        var message = errorPointer == IntPtr.Zero ? "Unknown native error." : Marshal.PtrToStringUTF8(errorPointer);
        throw new InvalidOperationException($"OpenRCT2 adapter failed with status {status}: {message}");
    }

    public void Dispose()
    {
        if (_disposed) return;

        try {
            if (_context != IntPtr.Zero)
                ThrowIfFailed(_shutdown(_context));
        }
        finally {
            _context = IntPtr.Zero;
            _disposed = true;
            NativeLibrary.Free(_nativeHandle);
        }
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RCTTileSurfaceData
{
    public uint TerrainSurfaceIdHash;
    public byte BaseZ;
    public byte SlopeFlags;
    public byte NorthCornerHeight;
    public byte EastCornerHeight;
    public byte SouthCornerHeight;
    public byte WestCornerHeight;
    public ushort WaterHeight;
    public byte GrassState;
    public byte CanGrowGrass;
    public byte Reserved0;
    public byte Reserved1;
}

public enum RCTPathSupportOrientation : byte
{
    NorthEastSouthWest = 0,
    NorthWestSouthEast = 1,
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RCTTilePathData
{
    public int TileX;
    public int TileY;
    public uint SurfaceIdHash;
    public uint RailingsIdHash;
    public byte BaseZ;
    public byte SlopeDirection;
    public byte Edges;
    public byte Corners;
    public byte Flags;
    public byte RailingsSupportType;
    public RCTPathSupportOrientation SupportOrientation;
    public uint AdditionIdHash;
    public byte AdditionFlags;

    public bool IsSloped => (Flags & 1) != 0;
    public bool IsQueue => (Flags & 2) != 0;
    public bool IsLampAddition => (AdditionFlags & 1) != 0;
    public bool AdditionIsBroken => (AdditionFlags & 2) != 0;
}
