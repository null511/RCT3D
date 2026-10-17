using ImGuiNET;
using NativeFileDialogSharp;
using System.Numerics;

namespace RCT3DViewer.Render;

internal sealed class ViewerUiController
{
    private string? _loadedParkPath;
    private string? _parkLoadError;
    private bool _showSkyModal;
    private bool _showParkLoadError;
    private float _timeOfDayHours = 12.0f;
    private float _sunAngleDegrees = 45.0f;
    private float _displayedFps;

    public float TimeOfDayHours => _timeOfDayHours;
    public float SunAngleDegrees => _sunAngleDegrees;
    public string? LoadedParkPath => _loadedParkPath;
    public bool HasParkLoadError => _parkLoadError is not null;


    public void SetLoadedParkPath(string parkPath)
    {
        _loadedParkPath = parkPath;
    }

    public void BeginParkLoad()
    {
        _parkLoadError = null;
    }

    public void ShowParkLoadError(string? message)
    {
        _parkLoadError = message;
        _showParkLoadError = true;
    }

    public void ClearParkLoadError()
    {
        _parkLoadError = null;
        _showParkLoadError = false;
    }

    public void BuildFrameUi(double deltaTime, Action<string> loadPark, Action requestExit)
    {
        var instantFps = deltaTime > 0.0 ? (float)(1.0 / deltaTime) : 0.0f;
        var smoothing = 1.0f - MathF.Exp(-Math.Max((float)deltaTime, 0.0f) * 5.0f);
        _displayedFps = _displayedFps <= 0.0f ? instantFps : float.Lerp(_displayedFps, instantFps, smoothing);

        if (ImGui.BeginMainMenuBar()) {
            if (ImGui.BeginMenu("File")) {
                if (ImGui.MenuItem("Load"))
                    OpenParkPicker(loadPark);
                if (ImGui.MenuItem("Exit"))
                    requestExit();
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
                ImGui.SliderFloat("Time of day", ref _timeOfDayHours, 0.0f, 24.0f, "%.1f h");
                ImGui.SliderFloat("Sun angle", ref _sunAngleDegrees, 0.0f, 360.0f, "%.0f°");
                if (ImGui.Button("Close"))
                    ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }

            var fpsText = $"FPS: {_displayedFps:0}";
            ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - ImGui.CalcTextSize(fpsText).X - ImGui.GetStyle().WindowPadding.X));
            ImGui.TextUnformatted(fpsText);
            ImGui.EndMainMenuBar();
        }

        if (_showParkLoadError && ImGui.Begin("Park load error", ref _showParkLoadError, ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.TextWrapped(_parkLoadError ?? "The park could not be loaded.");
            if (ImGui.Button("OK"))
                _showParkLoadError = false;
            ImGui.End();
        }
    }

    private void OpenParkPicker(Action<string> loadPark)
    {
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
