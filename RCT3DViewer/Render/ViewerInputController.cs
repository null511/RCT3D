using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace RCT3D.Render;

internal sealed class ViewerInputController : IDisposable
{
    private readonly IInputContext inputContext;
    private readonly FlightCamera camera;


    public ViewerInputController(IWindow window, FlightCamera camera, Func<bool> wantCaptureMouse)
    {
        this.camera = camera;

        inputContext = window.CreateInput();

        foreach (var mouse in inputContext.Mice) {
            mouse.MouseMove += (m, pos) => {
                if (m.IsButtonPressed(MouseButton.Right) && !wantCaptureMouse()) {
                    this.camera.HandleMouse(new Vector2(pos.X, pos.Y));
                    m.Cursor.CursorMode = CursorMode.Raw;
                }
                else {
                    m.Cursor.CursorMode = CursorMode.Normal;
                }
            };

            mouse.Scroll += (_, scroll) => {
                if (!wantCaptureMouse())
                    this.camera.HandleScroll(scroll.Y);
            };
        }
    }

    public void Update(double deltaTime, ImGuiVulkanRenderer? imguiRenderer)
    {
        if (imguiRenderer is not null) {
            var io = ImGui.GetIO();

            if (inputContext.Mice.Count > 0) {
                var mouse = inputContext.Mice[0];
                var position = mouse.Position;

                io.AddMousePosEvent(position.X, position.Y);
                io.AddMouseButtonEvent(0, mouse.IsButtonPressed(MouseButton.Left));
                io.AddMouseButtonEvent(1, mouse.IsButtonPressed(MouseButton.Right));
            }

            if (inputContext.Keyboards.Count > 0) {
                var keyboard = inputContext.Keyboards[0];

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

        if (inputContext.Keyboards.Count > 0 && !(imguiRenderer?.WantCaptureKeyboard ?? false))
            camera.HandleKeyboard(inputContext.Keyboards[0], (float)deltaTime);
    }

    public void Dispose()
    {
        inputContext.Dispose();
    }
}
