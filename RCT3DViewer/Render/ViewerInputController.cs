using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Windowing;
using System.Numerics;

namespace RCT3DViewer.Render;

internal sealed class ViewerInputController : IDisposable
{
    private readonly IInputContext _inputContext;
    private readonly FlightCamera _camera;


    public ViewerInputController(IWindow window, FlightCamera camera, Func<bool> wantCaptureMouse)
    {
        _camera = camera;
        _inputContext = window.CreateInput();

        foreach (var mouse in _inputContext.Mice) {
            mouse.MouseMove += (m, pos) => {
                if (m.IsButtonPressed(MouseButton.Right) && !wantCaptureMouse()) {
                    _camera.HandleMouse(new Vector2(pos.X, pos.Y));
                    m.Cursor.CursorMode = CursorMode.Raw;
                }
                else {
                    m.Cursor.CursorMode = CursorMode.Normal;
                }
            };
        }
    }

    public void Update(double deltaTime, ImGuiVulkanRenderer? imguiRenderer)
    {
        if (imguiRenderer is not null) {
            var io = ImGui.GetIO();
            if (_inputContext.Mice.Count > 0) {
                var mouse = _inputContext.Mice[0];
                var position = mouse.Position;
                io.AddMousePosEvent(position.X, position.Y);
                io.AddMouseButtonEvent(0, mouse.IsButtonPressed(MouseButton.Left));
                io.AddMouseButtonEvent(1, mouse.IsButtonPressed(MouseButton.Right));
            }

            if (_inputContext.Keyboards.Count > 0) {
                var keyboard = _inputContext.Keyboards[0];
                io.AddKeyEvent(ImGuiKey.Tab, keyboard.IsKeyPressed(Key.Tab));
                io.AddKeyEvent(ImGuiKey.LeftArrow, keyboard.IsKeyPressed(Key.Left));
                io.AddKeyEvent(ImGuiKey.RightArrow, keyboard.IsKeyPressed(Key.Right));
                io.AddKeyEvent(ImGuiKey.UpArrow, keyboard.IsKeyPressed(Key.Up));
                io.AddKeyEvent(ImGuiKey.DownArrow, keyboard.IsKeyPressed(Key.Down));
                io.AddKeyEvent(ImGuiKey.Enter, keyboard.IsKeyPressed(Key.Enter));
                io.AddKeyEvent(ImGuiKey.Escape, keyboard.IsKeyPressed(Key.Escape));
                io.AddKeyEvent(ImGuiKey.LeftCtrl, keyboard.IsKeyPressed(Key.ControlLeft));
                io.AddKeyEvent(ImGuiKey.LeftShift, keyboard.IsKeyPressed(Key.ShiftLeft));
            }
        }

        if (_inputContext.Keyboards.Count > 0 && !(imguiRenderer?.WantCaptureKeyboard ?? false))
            _camera.HandleKeyboard(_inputContext.Keyboards[0], (float)deltaTime);
    }

    public void Dispose()
    {
        _inputContext.Dispose();
    }
}
