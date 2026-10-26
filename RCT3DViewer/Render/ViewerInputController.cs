using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Windowing;
using System.Numerics;

namespace RCT3D.Render;

internal sealed class ViewerInputController : IDisposable
{
    private readonly IInputContext inputContext;
    private readonly FlightCamera camera;
    private readonly PeepModeController peepMode;
    private readonly Action togglePause;
    private readonly Action toggleDebug;
    private bool f9WasPressed;
    private bool f3WasPressed;
    private bool pWasPressed;
    private bool eWasPressed;


    public ViewerInputController(IWindow window, FlightCamera camera, PeepModeController peepMode, Func<bool> wantCaptureMouse, Action togglePause, Action toggleDebug)
    {
        this.camera = camera;
        this.peepMode = peepMode;
        this.togglePause = togglePause;
        this.toggleDebug = toggleDebug;

        inputContext = window.CreateInput();

        foreach (var mouse in inputContext.Mice) {
            mouse.MouseDown += (_, button) => {
                if (button == MouseButton.Right)
                    this.camera.ResetMouseTracking();
            };

            mouse.MouseUp += (_, button) => {
                if (button == MouseButton.Right)
                    this.camera.ResetMouseTracking();
            };

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
        if (inputContext.Keyboards.Count > 0) {
            var keyboard = inputContext.Keyboards[0];
            var f9Pressed = keyboard.IsKeyPressed(Key.F9);
            if (f9Pressed && !f9WasPressed)
                togglePause();
            f9WasPressed = f9Pressed;

            var f3Pressed = keyboard.IsKeyPressed(Key.F3);
            if (f3Pressed && !f3WasPressed)
                toggleDebug();
            f3WasPressed = f3Pressed;

            var pPressed = keyboard.IsKeyPressed(Key.P);
            if (pPressed && !pWasPressed)
                peepMode.Toggle();
            pWasPressed = pPressed;

            var ePressed = keyboard.IsKeyPressed(Key.E);
            if (ePressed && !eWasPressed)
                peepMode.Interact();
            eWasPressed = ePressed;

            peepMode.Update(keyboard, (float)deltaTime, !(imguiRenderer?.WantCaptureKeyboard ?? false));
        }

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

        if (inputContext.Keyboards.Count > 0 && !(imguiRenderer?.WantCaptureKeyboard ?? false)
            && !peepMode.IsEnabled && !peepMode.IsEntering)
            camera.HandleKeyboard(inputContext.Keyboards[0], (float)deltaTime);
    }

    public void Dispose()
    {
        inputContext.Dispose();
    }
}
