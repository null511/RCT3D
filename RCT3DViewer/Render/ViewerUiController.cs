using ImGuiNET;
using NativeFileDialogSharp;
using System.Numerics;

namespace RCT3DViewer.Render;

internal sealed class ViewerUiController
{
    private string? _loadedParkPath;
    private string? _parkLoadError;
    private string? _loadingParkPath;
    private string _parkLoadStatus = string.Empty;
    private bool _isParkLoading;
    private bool _showSkyModal;
    private bool _showGraphicsOptions;
    private bool _showParkLoadError;
    private bool _useGameTime = true;
    private float _timeOfDayHours = 9.0f;
    private float _azimuthDegrees = 25.0f;
    private float _zenithDegrees;
    private float? _gameTimeOfDayHours;
    private float _displayedFps;
    private double _displayedFrameTimeMilliseconds;
    private bool _wireframeEnabled;
    private bool _taaEnabled = true;
    private float _sharpening = 0.5f;
    private float _mipBias = -1.0f;

    public float TimeOfDayHours => _timeOfDayHours;
    public float EffectiveTimeOfDayHours => _useGameTime && _gameTimeOfDayHours is float gameTime
        ? gameTime
        : _timeOfDayHours;
    public float AzimuthDegrees => _azimuthDegrees;
    public float ZenithDegrees => _zenithDegrees;
    public bool WireframeEnabled => _wireframeEnabled;
    public bool TaaEnabled => _taaEnabled;
    public float Sharpening => _sharpening;
    public float MipBias => _mipBias;
    //public string? LoadedParkPath => _loadedParkPath;
    //public bool HasParkLoadError => _parkLoadError is not null;
    public bool IsParkLoading => _isParkLoading;


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

    public void BuildFrameUi(double deltaTime, double activeFrameTimeMilliseconds, float? gameTimeOfDayHours, Action<string> loadPark, Action requestExit, bool wireframeAvailable = true)
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
            if (ImGui.BeginMenu("File")) {
                if (ImGui.MenuItem("Load", string.Empty, false, !_isParkLoading))
                    OpenParkPicker(loadPark);
                if (ImGui.MenuItem("Exit"))
                    requestExit();
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("View")) {
                ImGui.MenuItem("Graphics Options", string.Empty, ref _showGraphicsOptions);
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
            var graphicsOptionsVisible = ImGui.Begin("Graphics Options", ref _showGraphicsOptions, ImGuiWindowFlags.AlwaysAutoResize);
            if (graphicsOptionsVisible) {
                ImGui.BeginDisabled(!wireframeAvailable);
                ImGui.Checkbox("Wireframe", ref _wireframeEnabled);
                ImGui.EndDisabled();
                ImGui.Checkbox("TAA", ref _taaEnabled);
                ImGui.SliderFloat("Sharpening", ref _sharpening, 0.0f, 1.0f, "%.2f");
                ImGui.SliderFloat("Mip Bias", ref _mipBias, -2.0f, 0.0f, "%.2f");
            }
            ImGui.End();
        }

        if (_isParkLoading)
            DrawParkLoadingPanel();
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
