using RCT3D.Interop.Models;
using System.Runtime.InteropServices;
using System.Text;

namespace RCT3D.Interop;

public sealed unsafe class LibOpenRCT2 : IDisposable
{
    private const string LibName = "libopenrct2";
    private const uint ExpectedAbiVersion = 43;
    private const int ExpectedTrackElementDataSize = 36;
    private const int ExpectedPeepDataSize = 26;

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
    private delegate int GetTreeCountDelegate(IntPtr context, out ulong count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CopyTreeDataDelegate(IntPtr context, RCTTreeSceneryData* output, ulong capacity);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetObstacleCountDelegate(IntPtr context, out ulong count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CopyObstacleDataDelegate(IntPtr context, RCTTileObstacleData* output, ulong capacity);

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
    private delegate int CopyVehiclePathSamplesDelegate(IntPtr context, RCTVehiclePathSample* output, ulong capacity);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetVehicleLookAheadDelegate(IntPtr context, ushort entityId, float distance,
        out RCTVehiclePathSample start, out RCTVehiclePathSample end, out float amount, out int available);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CreatePeepDelegate(IntPtr context, int x, int y, int z, out ushort entityId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int MovePeepDelegate(IntPtr context, ushort entityId, int x, int y, int z, byte direction);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TakeControlPeepDelegate(IntPtr context, ushort entityId, int x, int y, int z, byte direction);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ReleasePeepDelegate(IntPtr context, ushort entityId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnterRideDelegate(IntPtr context, ushort entityId, ushort rideId, byte stationIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetPeepDataDelegate(IntPtr context, ushort entityId, RCTPeepData* output);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RemovePeepDelegate(IntPtr context, ushort entityId);

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
    private readonly GetTreeCountDelegate _getTreeCount;
    private readonly CopyTreeDataDelegate _copyTreeData;
    private readonly GetObstacleCountDelegate _getObstacleCount;
    private readonly CopyObstacleDataDelegate _copyObstacleData;
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
    private readonly GetVehicleCountDelegate _getVehiclePathSampleCount;
    private readonly CopyVehiclePathSamplesDelegate _copyVehiclePathSamples;
    private readonly GetVehicleLookAheadDelegate _getVehicleLookAhead;
    private readonly CreatePeepDelegate _createPeep;
    private readonly MovePeepDelegate _movePeep;
    private readonly TakeControlPeepDelegate _takeControlPeep;
    private readonly ReleasePeepDelegate _releasePeep;
    private readonly EnterRideDelegate _enterRide;
    private readonly GetPeepDataDelegate _getPeepData;
    private readonly RemovePeepDelegate _removePeep;
    private readonly GetLastErrorDelegate _getLastError;
    private readonly ShutdownDelegate _shutdown;
    private readonly string _libraryPath;
    private IntPtr _context;
    private bool _disposed;

    public static string DefaultLibraryPath => Path.Combine(AppContext.BaseDirectory,
        LibName + (OperatingSystem.IsWindows() ? ".dll" : OperatingSystem.IsLinux() ? ".so" : ".dylib"));
    //public bool IsAvailable => !_disposed && _context != IntPtr.Zero;


    public LibOpenRCT2(string libraryPath, string openRCT2Path, string rct2Path, string rct1Path)
    {
        var trackElementDataSize = Marshal.SizeOf<RCTTrackElementData>();
        if (trackElementDataSize != ExpectedTrackElementDataSize)
            throw new InvalidOperationException($"Managed track element data has size {trackElementDataSize}; expected {ExpectedTrackElementDataSize} bytes for adapter ABI {ExpectedAbiVersion}.");

        var peepDataSize = Marshal.SizeOf<RCTPeepData>();
        if (peepDataSize != ExpectedPeepDataSize)
            throw new InvalidOperationException($"Managed peep data has size {peepDataSize}; expected {ExpectedPeepDataSize} bytes for adapter ABI {ExpectedAbiVersion}.");

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
            
            if (Marshal.SizeOf<RCTTilePathData>() != 29)
                throw new InvalidOperationException("RCTTilePathData must be 29 bytes to match the OpenRCT2 adapter ABI.");

            if (Marshal.SizeOf<RCTTreeSceneryData>() != 20)
                throw new InvalidOperationException("RCTTreeSceneryData must be 20 bytes to match the OpenRCT2 adapter ABI.");

            if (Marshal.SizeOf<RCTTileObstacleData>() != 10)
                throw new InvalidOperationException("RCTTileObstacleData must be 10 bytes to match the OpenRCT2 adapter ABI.");
            
            if (Marshal.SizeOf<RCTVehicleData>() != 72
                || Marshal.OffsetOf<RCTVehicleData>(nameof(RCTVehicleData.ObjectIdHash)).ToInt32() != 60
                || Marshal.OffsetOf<RCTVehicleData>(nameof(RCTVehicleData.RotationFrameMask)).ToInt32() != 66
                || Marshal.OffsetOf<RCTVehicleData>(nameof(RCTVehicleData.SpinPosition)).ToInt32() != 68
                || Marshal.OffsetOf<RCTVehicleData>(nameof(RCTVehicleData.HasIndependentSpin)).ToInt32() != 69)
                throw new InvalidOperationException("RCTVehicleData must match the 72-byte OpenRCT2 adapter ABI layout.");

            if (Marshal.SizeOf<RCTRideEntranceData>() != 15)
                throw new InvalidOperationException("RCTRideEntranceData must be 15 bytes to match the OpenRCT2 adapter ABI.");

            if (Marshal.SizeOf<RCTPaletteEntry>() != 4)
                throw new InvalidOperationException("RCTPaletteEntry must be 4 bytes to match the OpenRCT2 adapter ABI.");

            var initialize = Bind<InitializeDelegate>("rct3d_openrct2_initialize");
            if (Marshal.SizeOf<RCTVehiclePathSample>() != 28)
                throw new InvalidOperationException("RCTVehiclePathSample must be 28 bytes to match the OpenRCT2 adapter ABI.");
            _loadPark = Bind<LoadParkDelegate>("rct3d_openrct2_load_park");
            _getMapDimensions = Bind<GetMapDimensionsDelegate>("rct3d_openrct2_get_map_dimensions");
            _copySurfaceData = Bind<CopySurfaceDataDelegate>("rct3d_openrct2_copy_surface_data");
            _getPathCount = Bind<GetPathCountDelegate>("rct3d_openrct2_get_path_count");
            _copyPathData = Bind<CopyPathDataDelegate>("rct3d_openrct2_copy_path_data");
            _getTreeCount = Bind<GetTreeCountDelegate>("rct3d_openrct2_get_tree_count");
            _copyTreeData = Bind<CopyTreeDataDelegate>("rct3d_openrct2_copy_tree_data");
            _getObstacleCount = Bind<GetObstacleCountDelegate>("rct3d_openrct2_get_obstacle_count");
            _copyObstacleData = Bind<CopyObstacleDataDelegate>("rct3d_openrct2_copy_obstacle_data");
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
            _getVehiclePathSampleCount = Bind<GetVehicleCountDelegate>("rct3d_openrct2_get_vehicle_path_sample_count");
            _copyVehiclePathSamples = Bind<CopyVehiclePathSamplesDelegate>("rct3d_openrct2_copy_vehicle_path_samples");
            _getVehicleLookAhead = Bind<GetVehicleLookAheadDelegate>("rct3d_openrct2_get_vehicle_look_ahead");
            _createPeep = Bind<CreatePeepDelegate>("rct3d_openrct2_create_peep");
            _movePeep = Bind<MovePeepDelegate>("rct3d_openrct2_move_peep");
            _takeControlPeep = Bind<TakeControlPeepDelegate>("rct3d_openrct2_take_control_peep");
            _releasePeep = Bind<ReleasePeepDelegate>("rct3d_openrct2_release_peep");
            _enterRide = Bind<EnterRideDelegate>("rct3d_openrct2_enter_ride");
            _getPeepData = Bind<GetPeepDataDelegate>("rct3d_openrct2_get_peep_data");
            _removePeep = Bind<RemovePeepDelegate>("rct3d_openrct2_remove_peep");
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

    public ulong GetTreeCount()
    {
        ThrowIfFailed(_getTreeCount(_context, out var count));
        return count;
    }

    public void CopyTreeData(Span<RCTTreeSceneryData> output)
    {
        fixed (RCTTreeSceneryData* pointer = output)
            ThrowIfFailed(_copyTreeData(_context, pointer, (ulong)output.Length));
    }

    public ulong GetObstacleCount()
    {
        ThrowIfFailed(_getObstacleCount(_context, out var count));
        return count;
    }

    public void CopyObstacleData(Span<RCTTileObstacleData> output)
    {
        fixed (RCTTileObstacleData* pointer = output)
            ThrowIfFailed(_copyObstacleData(_context, pointer, (ulong)output.Length));
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

    public RCTVehiclePathSample[] ReadVehiclePathSamples()
    {
        ThrowIfFailed(_getVehiclePathSampleCount(_context, out var count));
        if (count > int.MaxValue)
            throw new InvalidOperationException($"OpenRCT2 returned too many vehicle path samples: {count}.");
        var samples = new RCTVehiclePathSample[(int)count];
        fixed (RCTVehiclePathSample* pointer = samples)
            ThrowIfFailed(_copyVehiclePathSamples(_context, pointer, count));
        return samples;
    }

    public bool TryGetVehicleLookAhead(ushort entityId, float nativeDistance,
        out RCTVehiclePathSample start, out RCTVehiclePathSample end, out float amount)
    {
        ThrowIfFailed(_getVehicleLookAhead(_context, entityId, nativeDistance, out start, out end, out amount, out var available));
        return available != 0;
    }

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

    public ushort CreatePeep(int x, int y, int z)
    {
        ThrowIfFailed(_createPeep(_context, x, y, z, out var entityId));
        return entityId;
    }

    public void MovePeep(ushort entityId, int x, int y, int z, byte direction) =>
        ThrowIfFailed(_movePeep(_context, entityId, x, y, z, direction));

    public void TakeControlPeep(ushort entityId, int x, int y, int z, byte direction) =>
        ThrowIfFailed(_takeControlPeep(_context, entityId, x, y, z, direction));

    public void ReleasePeep(ushort entityId) => ThrowIfFailed(_releasePeep(_context, entityId));

    public void EnterRide(ushort entityId, ushort rideId, byte stationIndex) =>
        ThrowIfFailed(_enterRide(_context, entityId, rideId, stationIndex));

    public RCTPeepData GetPeepData(ushort entityId)
    {
        RCTPeepData output = default;
        ThrowIfFailed(_getPeepData(_context, entityId, &output));
        return output;
    }

    public void RemovePeep(ushort entityId) => ThrowIfFailed(_removePeep(_context, entityId));

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
