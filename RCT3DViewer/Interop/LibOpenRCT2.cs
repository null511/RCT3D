using System.Runtime.InteropServices;
using System.Text;

namespace RCT3D.Interop;

public sealed unsafe class LibOpenRCT2 : IDisposable
{
    private const string LibName = "libopenrct2";
    private const uint ExpectedAbiVersion = 22;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint GetAbiVersionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitializeDelegate(byte* openRCT2Path, byte* rct2Path, byte* rct1Path, out IntPtr context);

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
    private delegate int GetTrackCountDelegate(IntPtr context, out ulong count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CopyTrackDataDelegate(IntPtr context, RCTTrackElementData* output, ulong capacity);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetRideEntranceCountDelegate(IntPtr context, out ulong count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CopyRideEntranceDataDelegate(IntPtr context, RCTRideEntranceData* output, ulong capacity);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CopyPaletteDataDelegate(IntPtr context, RCTPaletteEntry* output, ulong capacity);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int AdvanceSimulationDelegate(IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PauseStateDelegate(IntPtr context, int* isPaused);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetTimeOfDayHoursDelegate(IntPtr context, float* hours);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetVehicleCountDelegate(IntPtr context, out ulong count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CopyVehicleDataDelegate(IntPtr context, RCTVehicleData* output, ulong capacity);

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
    private readonly GetTrackCountDelegate _getTrackCount;
    private readonly CopyTrackDataDelegate _copyTrackData;
    private readonly GetRideEntranceCountDelegate _getRideEntranceCount;
    private readonly CopyRideEntranceDataDelegate _copyRideEntranceData;
    private readonly CopyPaletteDataDelegate _copyPaletteData;
    private readonly AdvanceSimulationDelegate _advanceSimulation;
    private readonly PauseStateDelegate _getPauseState;
    private readonly PauseStateDelegate _togglePause;
    private readonly GetTimeOfDayHoursDelegate _getTimeOfDayHours;
    private readonly GetVehicleCountDelegate _getVehicleCount;
    private readonly CopyVehicleDataDelegate _copyVehicleData;
    private readonly GetLastErrorDelegate _getLastError;
    private readonly ShutdownDelegate _shutdown;
    private readonly string _libraryPath;
    private IntPtr _context;
    private bool _disposed;

    public static string DefaultLibraryPath => Path.Combine(AppContext.BaseDirectory, LibName + ".dll");
    //public bool IsAvailable => !_disposed && _context != IntPtr.Zero;


    public LibOpenRCT2(string libraryPath, string openRCT2Path, string rct2Path, string rct1Path)
    {
        _libraryPath = Path.GetFullPath(libraryPath);
        if (!NativeLoader.TryLoadNativeLibrary(_libraryPath, out _nativeHandle, out var error))
            throw new InvalidOperationException(error);

        try {
            var getAbiVersion = Bind<GetAbiVersionDelegate>("rct3d_openrct2_get_abi_version");
            var version = getAbiVersion();

            if (version != ExpectedAbiVersion)
                throw new InvalidOperationException($"Unsupported OpenRCT2 adapter ABI version {version}; expected {ExpectedAbiVersion}. Loaded library: '{_libraryPath}'.");
            
            if (Marshal.SizeOf<RCTTileSurfaceData>() != 20)
                throw new InvalidOperationException("RCTTileSurfaceData must be 20 bytes to match the OpenRCT2 adapter ABI.");
            
            if (Marshal.SizeOf<RCTTilePathData>() != 28)
                throw new InvalidOperationException("RCTTilePathData must be 28 bytes to match the OpenRCT2 adapter ABI.");
            
            if (Marshal.SizeOf<RCTVehicleData>() != 28)
                throw new InvalidOperationException("RCTVehicleData must be 28 bytes to match the OpenRCT2 adapter ABI.");

            if (Marshal.SizeOf<RCTTrackElementData>() != 30)
                throw new InvalidOperationException("RCTTrackElementData must be 30 bytes to match the OpenRCT2 adapter ABI.");

            if (Marshal.SizeOf<RCTRideEntranceData>() != 14)
                throw new InvalidOperationException("RCTRideEntranceData must be 14 bytes to match the OpenRCT2 adapter ABI.");

            if (Marshal.SizeOf<RCTPaletteEntry>() != 4)
                throw new InvalidOperationException("RCTPaletteEntry must be 4 bytes to match the OpenRCT2 adapter ABI.");

            var initialize = Bind<InitializeDelegate>("rct3d_openrct2_initialize");
            _loadPark = Bind<LoadParkDelegate>("rct3d_openrct2_load_park");
            _getMapDimensions = Bind<GetMapDimensionsDelegate>("rct3d_openrct2_get_map_dimensions");
            _copySurfaceData = Bind<CopySurfaceDataDelegate>("rct3d_openrct2_copy_surface_data");
            _getPathCount = Bind<GetPathCountDelegate>("rct3d_openrct2_get_path_count");
            _copyPathData = Bind<CopyPathDataDelegate>("rct3d_openrct2_copy_path_data");
            _getTrackCount = Bind<GetTrackCountDelegate>("rct3d_openrct2_get_track_count");
            _copyTrackData = Bind<CopyTrackDataDelegate>("rct3d_openrct2_copy_track_data");
            _getRideEntranceCount = Bind<GetRideEntranceCountDelegate>("rct3d_openrct2_get_ride_entrance_count");
            _copyRideEntranceData = Bind<CopyRideEntranceDataDelegate>("rct3d_openrct2_copy_ride_entrance_data");
            _copyPaletteData = Bind<CopyPaletteDataDelegate>("rct3d_openrct2_copy_palette_data");
            _advanceSimulation = Bind<AdvanceSimulationDelegate>("rct3d_openrct2_advance_simulation");
            _getPauseState = Bind<PauseStateDelegate>("rct3d_openrct2_get_pause_state");
            _togglePause = Bind<PauseStateDelegate>("rct3d_openrct2_toggle_pause");
            _getTimeOfDayHours = Bind<GetTimeOfDayHoursDelegate>("rct3d_openrct2_get_time_of_day_hours");
            _getVehicleCount = Bind<GetVehicleCountDelegate>("rct3d_openrct2_get_vehicle_count");
            _copyVehicleData = Bind<CopyVehicleDataDelegate>("rct3d_openrct2_copy_vehicle_data");
            _getLastError = Bind<GetLastErrorDelegate>("rct3d_openrct2_get_last_error");
            _shutdown = Bind<ShutdownDelegate>("rct3d_openrct2_shutdown");

            var openRCT2PathUtf8 = Encoding.UTF8.GetBytes(openRCT2Path + '\0');
            var rct2PathUtf8 = Encoding.UTF8.GetBytes(rct2Path + '\0');
            var rct1PathUtf8 = Encoding.UTF8.GetBytes(rct1Path + '\0');

            fixed (byte* openRCT2PathPointer = openRCT2PathUtf8)
            fixed (byte* rct2PathPointer = rct2PathUtf8)
            fixed (byte* rct1PathPointer = rct1PathUtf8) {
                var status = initialize(openRCT2PathPointer, rct2PathPointer, rct1PathPointer, out var context);
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

    public ulong GetTrackCount()
    {
        ThrowIfFailed(_getTrackCount(_context, out var count));
        return count;
    }

    public void CopyTrackData(Span<RCTTrackElementData> output)
    {
        fixed (RCTTrackElementData* pointer = output)
            ThrowIfFailed(_copyTrackData(_context, pointer, (ulong)output.Length));
    }

    public ulong GetRideEntranceCount()
    {
        ThrowIfFailed(_getRideEntranceCount(_context, out var count));
        return count;
    }

    public void CopyRideEntranceData(Span<RCTRideEntranceData> output)
    {
        fixed (RCTRideEntranceData* pointer = output)
            ThrowIfFailed(_copyRideEntranceData(_context, pointer, (ulong)output.Length));
    }

    public void CopyPaletteData(Span<RCTPaletteEntry> output)
    {
        fixed (RCTPaletteEntry* pointer = output)
            ThrowIfFailed(_copyPaletteData(_context, pointer, (ulong)output.Length));
    }

    public void AdvanceSimulation() => ThrowIfFailed(_advanceSimulation(_context));

    public bool GetPauseState()
    {
        int isPaused = 0;
        ThrowIfFailed(_getPauseState(_context, &isPaused));
        return isPaused != 0;
    }

    public bool TogglePause()
    {
        int isPaused = 0;
        ThrowIfFailed(_togglePause(_context, &isPaused));
        return isPaused != 0;
    }

    public float GetTimeOfDayHours()
    {
        float hours = 0.0f;
        ThrowIfFailed(_getTimeOfDayHours(_context, &hours));
        return hours;
    }

    public ulong GetVehicleCount()
    {
        ThrowIfFailed(_getVehicleCount(_context, out var count));
        return count;
    }

    public void CopyVehicleData(Span<RCTVehicleData> output)
    {
        fixed (RCTVehicleData* pointer = output)
            ThrowIfFailed(_copyVehicleData(_context, pointer, (ulong)output.Length));
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
    public uint TerrainEdgeIdHash;
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
    public bool IsBenchAddition => (AdditionFlags & 4) != 0;
}

[StructLayout(LayoutKind.Sequential)]
public struct RCTVehicleData
{
    public ushort EntityId;
    public int X;
    public int Y;
    public int Z;
    public byte Orientation;
    public byte Pitch;
    public byte Roll;
    public byte Reserved;
    public float PitchRadians;
    public float RollRadians;
}
