using ImGuiNET;
using NativeFileDialogSharp;
using RCT3D.Configuration;
using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render;

internal sealed class ViewerUiController
{
    private string? _loadedParkPath;
    private string? _parkLoadError;
    private string? _loadingParkPath;
    private string _parkLoadStatus = string.Empty;
    private bool _isParkLoading;
    private bool _showSkyModal;
    private bool _showGraphicsOptions;
    private bool _showDebug;
    private bool _showParkLoadError;
    private bool _useGameTime = false;
    private float _timeOfDayHours = 9.0f;
    private float _azimuthDegrees = 25.0f;
    private float _zenithDegrees = 18.0f;
    private float? _gameTimeOfDayHours;
    private float _displayedFps;
    private double _displayedFrameTimeMilliseconds;
    private bool _wireframeEnabled;
    private bool _taaEnabled = false;
    private int _debugView;
    private float _sharpening = 0.5f;
    private float _mipBias = -1.0f;
    private float _shadowDistortionFactor = 0.65f;
    private float _shadowRadius = ShadowRenderTargets.DefaultRadius;
    private uint _appliedShadowResolution = ShadowRenderTargets.DefaultResolution;
    private uint _selectedShadowResolution = ShadowRenderTargets.DefaultResolution;
    private string? _graphicsOptionsSaveError;
    private GraphicsOptionsSettings _appliedGraphicsOptions = GraphicsOptionsSettings.Defaults;
    private bool _editingGraphicsOptions;
    private static readonly string[] DebugViewOptions = { "None", "Albedo", "Geometry Normal", "Texture Normal" };
    private static readonly string[] ShadowResolutionOptions = { "512", "1024", "2048", "4096" };

    public ViewerUiController(GraphicsOptionsSettings settings)
    {
        _taaEnabled = settings.TaaEnabled;
        _debugView = settings.DebugView;
        _sharpening = settings.Sharpening;
        _mipBias = settings.MipBias;
        _shadowDistortionFactor = settings.ShadowDistortionFactor;
        _shadowRadius = settings.ShadowRadius;
        _appliedShadowResolution = settings.ShadowResolution;
        _selectedShadowResolution = settings.ShadowResolution;
        _appliedGraphicsOptions = settings;
    }

    //public float TimeOfDayHours => _timeOfDayHours;
    public float EffectiveTimeOfDayHours => _useGameTime && _gameTimeOfDayHours is float gameTime
        ? gameTime
        : _timeOfDayHours;
    public float AzimuthDegrees => _azimuthDegrees;
    public float ZenithDegrees => _zenithDegrees;
    public bool WireframeEnabled => _wireframeEnabled;
    public bool TaaEnabled => _taaEnabled;
    public uint DebugView => (uint)_debugView;
    public float Sharpening => _sharpening;
    public float MipBias => _mipBias;
    public float ShadowDistortionFactor => _shadowDistortionFactor;
    public float ShadowRadius => _shadowRadius;
    public uint ShadowResolution => _appliedShadowResolution;
    //public string? LoadedParkPath => _loadedParkPath;
    //public bool HasParkLoadError => _parkLoadError is not null;
    public bool IsParkLoading => _isParkLoading;

    private bool HasPendingGraphicsOptions => CaptureGraphicsOptions() != _appliedGraphicsOptions
        || _selectedShadowResolution != _appliedShadowResolution
        || _graphicsOptionsSaveError is not null;

    private void BeginGraphicsOptionsEdit()
    {
        _selectedShadowResolution = _appliedShadowResolution;
        _editingGraphicsOptions = true;
    }

    private void CancelGraphicsOptionsEdit()
    {
        _taaEnabled = _appliedGraphicsOptions.TaaEnabled;
        _debugView = _appliedGraphicsOptions.DebugView;
        _sharpening = _appliedGraphicsOptions.Sharpening;
        _mipBias = _appliedGraphicsOptions.MipBias;
        _shadowDistortionFactor = _appliedGraphicsOptions.ShadowDistortionFactor;
        _shadowRadius = _appliedGraphicsOptions.ShadowRadius;
        _selectedShadowResolution = _appliedShadowResolution;
        _editingGraphicsOptions = false;
    }

    private void ApplyGraphicsOptions(Action<uint, float> applyShadowSettings, Action<GraphicsOptionsSettings> saveGraphicsOptions)
    {
        applyShadowSettings(_selectedShadowResolution, _shadowRadius);

        _appliedShadowResolution = _selectedShadowResolution;
        _appliedGraphicsOptions = CaptureGraphicsOptions();
        _editingGraphicsOptions = false;

        try {
            saveGraphicsOptions(_appliedGraphicsOptions);
            _graphicsOptionsSaveError = null;
        }
        catch (Exception ex) {
            _graphicsOptionsSaveError = ex.Message;
        }
    }

    private GraphicsOptionsSettings CaptureGraphicsOptions() => new() {
        TaaEnabled = _taaEnabled,
        DebugView = _debugView,
        Sharpening = _sharpening,
        MipBias = _mipBias,
        ShadowResolution = _selectedShadowResolution,
        ShadowRadius = _shadowRadius,
        ShadowDistortionFactor = _shadowDistortionFactor
    };

    public void ToggleDebug() => _showDebug = !_showDebug;

    public void BeginParkLoad(string parkPath)
    {
        _isParkLoading = true;
        _loadingParkPath = parkPath;
        _parkLoadStatus = "Loading park file...";
        _parkLoadError = null;
        _showParkLoadError = false;
    }

    public void SetParkLoadStatus(string status) => _parkLoadStatus = status;

    public void CompleteParkLoad(string parkPath)
    {
        _loadedParkPath = parkPath;
        _loadingParkPath = null;
        _isParkLoading = false;
        _parkLoadStatus = string.Empty;
    }

    public void ShowParkLoadError(string? message)
    {
        _isParkLoading = false;
        _loadingParkPath = null;
        _parkLoadStatus = string.Empty;
        _parkLoadError = message;
        _showParkLoadError = true;
    }

    public void ClearParkAfterLoadFailure(string? message)
    {
        _loadedParkPath = null;
        ShowParkLoadError(message);
    }

    //public void ClearParkLoadError()
    //{
    //    _parkLoadError = null;
    //    _showParkLoadError = false;
    //}

    public void BuildFrameUi(double deltaTime, double activeFrameTimeMilliseconds, float? gameTimeOfDayHours, bool? isPaused, int cameraTileX, int cameraTileY,
        int visibleSections, int totalSections,
        IReadOnlyList<RCTTrackElementData> cameraTileTrackElements, Action<string> loadPark, Action togglePause, Action requestExit,
        bool peepModeEnabled, string? peepModeStatus, Action togglePeepMode, Action<uint, float> applyShadowSettings,
        Action<GraphicsOptionsSettings> saveGraphicsOptions, bool wireframeAvailable = true)
    {
        _gameTimeOfDayHours = gameTimeOfDayHours;
        var instantFps = deltaTime > 0.0 ? (float)(1.0 / deltaTime) : 0.0f;
        var smoothing = 1.0f - MathF.Exp(-Math.Max((float)deltaTime, 0.0f) * 5.0f);
        _displayedFps = _displayedFps <= 0.0f ? instantFps : float.Lerp(_displayedFps, instantFps, smoothing);
        if (activeFrameTimeMilliseconds > 0.0)
            _displayedFrameTimeMilliseconds = _displayedFrameTimeMilliseconds <= 0.0
                ? activeFrameTimeMilliseconds
                : _displayedFrameTimeMilliseconds + (activeFrameTimeMilliseconds - _displayedFrameTimeMilliseconds) * smoothing;

        if (ImGui.BeginMainMenuBar()) {
            if (ImGui.BeginMenu("Park")) {
                if (ImGui.MenuItem("Load", string.Empty, false, !_isParkLoading))
                    OpenParkPicker(loadPark);
                if (ImGui.MenuItem("Pause", string.Empty, isPaused == true, isPaused.HasValue))
                    togglePause();
                if (ImGui.MenuItem("Exit"))
                    requestExit();
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("View")) {
                if (ImGui.MenuItem("Wireframe", string.Empty, _wireframeEnabled, wireframeAvailable))
                    _wireframeEnabled = !_wireframeEnabled;
                if (ImGui.MenuItem("Peep Mode", "P", peepModeEnabled))
                    togglePeepMode();
                var graphicsOptionsWereOpen = _showGraphicsOptions;
                ImGui.MenuItem("Graphics Options", string.Empty, ref _showGraphicsOptions);
                if (!graphicsOptionsWereOpen && _showGraphicsOptions)
                    BeginGraphicsOptionsEdit();
                else if (graphicsOptionsWereOpen && !_showGraphicsOptions)
                    CancelGraphicsOptionsEdit();
                ImGui.MenuItem("Debug", string.Empty, ref _showDebug);
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("World")) {
                if (ImGui.MenuItem("Sky"))
                    _showSkyModal = true;
                ImGui.EndMenu();
            }

            // Open the modal from the main-menu-bar window, not the nested World
            // menu window. Popup IDs are scoped to the current ImGui window.
            if (_showSkyModal) {
                ImGui.OpenPopup("Sky settings");
                _showSkyModal = false;
            }

            ImGui.PushStyleColor(ImGuiCol.ModalWindowDimBg, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
            var skyModalVisible = ImGui.BeginPopupModal("Sky settings", ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.PopStyleColor();
            if (skyModalVisible) {
                ImGui.Checkbox("Use game time", ref _useGameTime);
                ImGui.BeginDisabled(_useGameTime);
                ImGui.SliderFloat("Time of day", ref _timeOfDayHours, 0.0f, 24.0f, "%.1f h");
                ImGui.EndDisabled();
                ImGui.SliderFloat("Azimuth", ref _azimuthDegrees, 0.0f, 360.0f, "%.0f°");
                ImGui.SliderFloat("Zenith", ref _zenithDegrees, 0.0f, 90.0f, "%.0f°");
                if (ImGui.Button("Close"))
                    ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }

            var fpsText = $"FPS: {_displayedFps:0} | Frame: {_displayedFrameTimeMilliseconds:0.00} ms";
            ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - ImGui.CalcTextSize(fpsText).X - ImGui.GetStyle().WindowPadding.X));
            ImGui.TextUnformatted(fpsText);
            ImGui.EndMainMenuBar();
        }

        if (_showDebug)
            DrawDebugOverlay(cameraTileX, cameraTileY, visibleSections, totalSections, cameraTileTrackElements);

        if (peepModeEnabled || peepModeStatus is not null)
            DrawPeepModeOverlay(peepModeEnabled, peepModeStatus);

        if (_showParkLoadError) {
            var errorWindowVisible = ImGui.Begin("Park load error", ref _showParkLoadError, ImGuiWindowFlags.AlwaysAutoResize);
            if (errorWindowVisible) {
                ImGui.TextWrapped(_parkLoadError ?? "The park could not be loaded.");
                if (ImGui.Button("OK"))
                    _showParkLoadError = false;
            }
            ImGui.End();
        }

        if (_showGraphicsOptions) {
            if (!_editingGraphicsOptions)
                BeginGraphicsOptionsEdit();
            var graphicsOptionsWereOpen = _showGraphicsOptions;
            var graphicsOptionsVisible = ImGui.Begin("Graphics Options", ref _showGraphicsOptions, ImGuiWindowFlags.AlwaysAutoResize);
            if (graphicsOptionsVisible) {
                ImGui.Checkbox("TAA", ref _taaEnabled);
                ImGui.Combo("Debug View", ref _debugView, DebugViewOptions, DebugViewOptions.Length);
                ImGui.SliderFloat("Sharpening", ref _sharpening, 0.0f, 1.0f, "%.2f");
                ImGui.SliderFloat("Mip Bias", ref _mipBias, -2.0f, 0.0f, "%.2f");
                ImGui.SeparatorText("Shadows");
                var shadowResolutionIndex = _selectedShadowResolution switch {
                    512 => 0,
                    1024 => 1,
                    2048 => 2,
                    4096 => 3,
                    _ => 2
                };
                if (ImGui.Combo("Resolution", ref shadowResolutionIndex, ShadowResolutionOptions, ShadowResolutionOptions.Length))
                    _selectedShadowResolution = (uint)(512 << shadowResolutionIndex);
                ImGui.SliderFloat("Radius", ref _shadowRadius, 16.0f, 512.0f, "%.0f");
                ImGui.SliderFloat("Shadow Distortion", ref _shadowDistortionFactor, 0.0f, 1.0f, "%.2f");
                if (_graphicsOptionsSaveError is not null)
                    ImGui.TextWrapped($"Settings are active but could not be saved: {_graphicsOptionsSaveError}");
                ImGui.Separator();
                ImGui.BeginDisabled(!HasPendingGraphicsOptions);
                if (ImGui.Button("Apply"))
                    ApplyGraphicsOptions(applyShadowSettings, saveGraphicsOptions);
                ImGui.EndDisabled();
                ImGui.SameLine();
                if (ImGui.Button("Cancel")) {
                    CancelGraphicsOptionsEdit();
                    _showGraphicsOptions = false;
                }
            }
            ImGui.End();
            if (graphicsOptionsWereOpen && !_showGraphicsOptions)
                CancelGraphicsOptionsEdit();
        }

        if (_isParkLoading)
            DrawParkLoadingPanel();
    }

    private static void DrawPeepModeOverlay(bool enabled, string? status)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(10.0f, viewport.WorkSize.Y - 10.0f), ImGuiCond.Always, new Vector2(0.0f, 1.0f));
        ImGui.SetNextWindowBgAlpha(0.75f);

        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.AlwaysAutoResize;
        if (ImGui.Begin("Peep Mode status", flags)) {
            if (status is not null)
                ImGui.TextWrapped(status);
            else if (enabled) {
                ImGui.TextUnformatted("Peep Mode | WASD walk | Shift run | E ride | P exit");
            }
        }
        ImGui.End();
    }

    private static void DrawDebugOverlay(int tileX, int tileY, int visibleSections, int totalSections,
        IReadOnlyList<RCTTrackElementData> trackElements)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(10.0f, 10.0f), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.75f);

        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.AlwaysAutoResize;
        if (ImGui.Begin("Debug overlay", flags)) {
            ImGui.TextUnformatted($"Tile: {tileX}, {tileY}");
            ImGui.TextUnformatted($"Sections: {visibleSections} / {totalSections}");
            if (trackElements.Count == 0) {
                ImGui.TextUnformatted("No ride track at this tile");
            }
            else {
                foreach (var track in trackElements) {
                    ImGui.TextUnformatted($"{track.Part} | sequence {track.Sequence}/{track.SequenceCount} | direction {track.Direction} | height {track.BaseHeight} | ride {track.RideId} ({track.RideType})");
                }
            }
        }
        ImGui.End();
    }

    private void DrawParkLoadingPanel()
    {
        var viewport = ImGui.GetMainViewport();

        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowBgAlpha(0.94f);
        if (ImGui.Begin("Loading park", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoInputs)) {
            ImGui.TextUnformatted("Loading park");
            ImGui.Separator();
            ImGui.TextWrapped(_parkLoadStatus);
            if (_loadingParkPath is not null)
                ImGui.TextDisabled(Path.GetFileName(_loadingParkPath));
        }
        ImGui.End();
    }

    private void OpenParkPicker(Action<string> loadPark)
    {
        if (_isParkLoading) return;

        var defaultPath = _loadedParkPath is null ? string.Empty : Path.GetDirectoryName(_loadedParkPath) ?? string.Empty;

        try {
            var result = Dialog.FileOpen("park", defaultPath);

            if (result.IsError) {
                ShowParkLoadError(result.ErrorMessage);
            }
            else if (result.IsOk) {
                loadPark(result.Path);
            }
        }
        catch (Exception ex) {
            ShowParkLoadError(ex.Message);
        }
    }
}
