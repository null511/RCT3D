using ImGuiNET;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NativeFileDialogSharp;
using RCT3D.Configuration;
using Silk.NET.Vulkan;

namespace RCT3D.Render;

internal sealed class MenuScreen : IViewerScreen
{
    private readonly ILogger logger;
    private readonly IConfiguration? configuration;
    private readonly GraphicsOptionsStore graphicsOptionsStore;
    private readonly GamePathSettingsStore pathStore = new();
    private readonly ViewerUiController ui;

    private GamePathSettings savedPaths;
    private string rct1Path = string.Empty;
    private string rct2Path = string.Empty;
    private string openRCT2Path = string.Empty;
    private string? configurationError;
    private bool showConfiguration;

    public Action<string>? LoadPark;
    public Action? RequestExit;


    public MenuScreen(
        ILogger<MenuScreen> logger,
        IConfiguration? configuration,
        GraphicsOptionsStore graphicsOptionsStore,
        ViewerUiController ui)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.graphicsOptionsStore = graphicsOptionsStore;
        this.ui = ui;

        savedPaths = pathStore.Load(out var error);

        if (error is not null)
            ui.ShowParkLoadError(error);
    }

    public GamePathSettings EffectivePaths => GamePathSettingsStore.Resolve(configuration, savedPaths);

    private void OpenConfiguration()
    {
        if (showConfiguration) return;

        var editable = GamePathSettingsStore.GetEditableSettings(configuration, savedPaths);

        rct1Path = editable.Rct1Path ?? string.Empty;
        rct2Path = editable.Rct2Path ?? string.Empty;
        openRCT2Path = editable.OpenRCT2Path ?? string.Empty;
        configurationError = null;
        showConfiguration = true;
    }

    public void CloseWindows()
    {
        showConfiguration = false;
        ui.CloseScreenWindows(ApplyFieldOfView);
    }

    public void PrepareFrame(double deltaTime, double activeFrameTimeMilliseconds)
    {
        ui.BuildMenuUi(LoadPark, OpenConfiguration, RequestExit, ApplyShadowSettings, ApplyFieldOfView, graphicsOptionsStore.Save);
        if (!showConfiguration) return;

        if (ImGui.Begin("Configuration", ref showConfiguration, ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.TextWrapped("Installation paths apply to the next park load. Relative paths use the application directory.");
            ImGui.BeginDisabled(ui.IsParkLoading);
            var effective = EffectivePaths;
            DrawPath("RCT1_PATH", ref rct1Path, effective.Rct1Path);
            DrawPath("RCT2_PATH", ref rct2Path, effective.Rct2Path);
            DrawPath("OPENRCT2_DATA", ref openRCT2Path, effective.OpenRCT2Path);
            if (configurationError is not null)
                ImGui.TextWrapped(configurationError);
            ImGui.Separator();
            if (ImGui.Button("Apply"))
                SavePaths();
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                showConfiguration = false;
            ImGui.EndDisabled();
        }
        ImGui.End();
    }

    private void DrawPath(string key, ref string value, string? effective)
    {
        ImGui.PushID(key);
        ImGui.SetNextItemWidth(500.0f);
        ImGui.InputText(key, ref value, 32768);
        ImGui.SameLine();
        if (ImGui.Button("Browse...")) {
            try {
                var defaultPath = string.IsNullOrWhiteSpace(value) ? string.Empty : GamePathSettingsStore.ResolveDirectoryPath(value);
                if (!Directory.Exists(defaultPath))
                    defaultPath = string.Empty;
                var result = Dialog.FolderPicker(defaultPath);
                if (result.IsError)
                    throw new InvalidOperationException(result.ErrorMessage);
                if (result.IsOk)
                    value = result.Path;
            }
            catch (Exception ex) {
                logger.LogError(ex, "Failed to select {PathSetting}", key);
                configurationError = ex.Message;
            }
        }
        var source = GamePathSettingsStore.GetSource(configuration, savedPaths, key);
        ImGui.TextWrapped($"Effective ({source}): {effective ?? "not configured"}");
        if (source == "Environment")
            ImGui.TextWrapped($"RCT3D_{key} overrides the saved value. Remove that environment override to use edits here.");
        ImGui.PopID();
    }

    private void SavePaths()
    {
        var settings = new GamePathSettings {
            Rct1Path = rct1Path,
            Rct2Path = rct2Path,
            OpenRCT2Path = openRCT2Path,
        };

        try {
            pathStore.Save(settings);

            savedPaths = settings;
            configurationError = null;
            showConfiguration = false;
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to apply installation paths");
            configurationError = ex.Message;
        }
    }

    private static void ApplyShadowSettings(uint resolution, float radius) { }
    private static void ApplyFieldOfView(float fieldOfView) { }

    public void Update(double deltaTime) { }
    public void BeforeRender() { }
    public void Record(CommandBuffer commandBuffer, uint imageIndex, uint currentFrame) { }
    public void DrawPresentation(CommandBuffer commandBuffer, uint currentFrame) { }
    public void FrameSubmitted() { }
    public void Resize(bool extentChanged) { }

    public void Dispose() => CloseWindows();
}
