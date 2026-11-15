using Silk.NET.Input;
using System.Numerics;

namespace RCT3D.Render.Park;

internal sealed class ParkInputController : IDisposable
{
    private readonly IInputContext input;
    private readonly FlightCamera camera;
    private readonly PeepModeController peepMode;
    private readonly ImGuiVulkanRenderer imgui;
    private readonly Action togglePause;
    private readonly Action toggleDebug;

    private bool f9WasPressed;
    private bool f3WasPressed;
    private bool pWasPressed;
    private bool eWasPressed;
    private bool waitForRelease = true;


    public ParkInputController(IInputContext input, FlightCamera camera, PeepModeController peepMode,
        ImGuiVulkanRenderer imgui, Action togglePause, Action toggleDebug)
    {
        this.input = input;
        this.camera = camera;
        this.peepMode = peepMode;
        this.imgui = imgui;
        this.togglePause = togglePause;
        this.toggleDebug = toggleDebug;

        foreach (var mouse in input.Mice) {
            mouse.MouseDown += OnMouseButton;
            mouse.MouseUp += OnMouseButton;
            mouse.MouseMove += OnMouseMove;
            mouse.Scroll += OnScroll;
        }
    }

    private void OnMouseButton(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Right)
            camera.ResetMouseTracking();
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (!waitForRelease && mouse.IsButtonPressed(MouseButton.Right) && !imgui.WantCaptureMouse) {
            camera.HandleMouse(position);
            mouse.Cursor.CursorMode = CursorMode.Raw;
        }
        else
            mouse.Cursor.CursorMode = CursorMode.Normal;
    }

    private void OnScroll(IMouse mouse, ScrollWheel scroll)
    {
        if (!waitForRelease && !imgui.WantCaptureMouse)
            camera.HandleScroll(scroll.Y);
    }

    public void Update(double deltaTime)
    {
        if (input.Keyboards.Count == 0) return;

        var keyboard = input.Keyboards[0];
        var f9 = keyboard.IsKeyPressed(Key.F9);
        var f3 = keyboard.IsKeyPressed(Key.F3);
        var p = keyboard.IsKeyPressed(Key.P);
        var e = keyboard.IsKeyPressed(Key.E);

        if (waitForRelease)
            waitForRelease = f9 || f3 || p || e || keyboard.IsKeyPressed(Key.W) || keyboard.IsKeyPressed(Key.A)
                || keyboard.IsKeyPressed(Key.S) || keyboard.IsKeyPressed(Key.D) || keyboard.IsKeyPressed(Key.Space)
                || keyboard.IsKeyPressed(Key.ControlLeft) || input.Mice.Any(mouse => mouse.IsButtonPressed(MouseButton.Right));

        var allowMovement = !waitForRelease && !imgui.WantCaptureKeyboard;
        if (allowMovement) {
            if (f9 && !f9WasPressed) togglePause();
            if (f3 && !f3WasPressed) toggleDebug();
            if (p && !pWasPressed) peepMode.Toggle();
            if (e && !eWasPressed) peepMode.Interact();
        }

        f9WasPressed = f9;
        f3WasPressed = f3;
        pWasPressed = p;
        eWasPressed = e;

        peepMode.Update(keyboard, (float)deltaTime, allowMovement);
        if (allowMovement && !peepMode.IsEnabled && !peepMode.IsEntering)
            camera.HandleKeyboard(keyboard, (float)deltaTime);
    }

    public void Dispose()
    {
        foreach (var mouse in input.Mice) {
            mouse.MouseDown -= OnMouseButton;
            mouse.MouseUp -= OnMouseButton;
            mouse.MouseMove -= OnMouseMove;
            mouse.Scroll -= OnScroll;
            mouse.Cursor.CursorMode = CursorMode.Normal;
        }

        camera.ResetMouseTracking();
    }
}
