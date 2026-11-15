using ImGuiNET;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;

namespace RCT3D.Render;

internal sealed class ViewerInputController : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetClipboardText(IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetClipboardText(IntPtr context, IntPtr text);

    private readonly GetClipboardText getClipboard;
    private readonly SetClipboardText setClipboard;
    private readonly VulkanRenderResources resources;
    private readonly ILogger<ViewerInputController> logger;

    private IInputContext? input;
    private IntPtr clipboardText;
    private bool isDisposed;
    private bool clipboardCallbacksAttached;

    public bool IsInitialized { get; private set; }

    public IInputContext Input {
        get {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            return input ?? throw new InvalidOperationException("Shared input has not been initialized by the window load event.");
        }
    }

    private static readonly (Key Key, ImGuiKey ImGuiKey)[] EditingKeys = [
        (Key.Tab, ImGuiKey.Tab), (Key.Left, ImGuiKey.LeftArrow), (Key.Right, ImGuiKey.RightArrow),
        (Key.Up, ImGuiKey.UpArrow), (Key.Down, ImGuiKey.DownArrow), (Key.Home, ImGuiKey.Home),
        (Key.End, ImGuiKey.End), (Key.PageUp, ImGuiKey.PageUp), (Key.PageDown, ImGuiKey.PageDown),
        (Key.Backspace, ImGuiKey.Backspace), (Key.Delete, ImGuiKey.Delete), (Key.Insert, ImGuiKey.Insert),
        (Key.Enter, ImGuiKey.Enter), (Key.Escape, ImGuiKey.Escape),
        (Key.ControlLeft, ImGuiKey.LeftCtrl), (Key.ControlRight, ImGuiKey.RightCtrl),
        (Key.ShiftLeft, ImGuiKey.LeftShift), (Key.ShiftRight, ImGuiKey.RightShift),
        (Key.AltLeft, ImGuiKey.LeftAlt), (Key.AltRight, ImGuiKey.RightAlt),
        (Key.SuperLeft, ImGuiKey.LeftSuper), (Key.SuperRight, ImGuiKey.RightSuper)
    ];

    public ViewerInputController(IWindow window, VulkanRenderResources resources, ILogger<ViewerInputController> logger)
    {
        Window = window;
        this.resources = resources;
        this.logger = logger;
        getClipboard = ReadClipboard;
        setClipboard = WriteClipboard;
    }

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        if (IsInitialized)
            throw new InvalidOperationException("Shared input is already initialized.");

        resources.EnsureInitialized();

        try {
            InitializeCore();
            IsInitialized = true;
        }
        catch {
            Dispose();
            throw;
        }
    }

    private void InitializeCore()
    {
        input = Window.CreateInput();

        foreach (var keyboard in Input.Keyboards)
            keyboard.KeyChar += OnCharacter;

        foreach (var mouse in Input.Mice)
            mouse.Scroll += OnScroll;

        Window.FocusChanged += OnFocusChanged;
        var platform = ImGui.GetPlatformIO();
        clipboardCallbacksAttached = true;
        platform.Platform_GetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(getClipboard);
        platform.Platform_SetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(setClipboard);
    }

    private IWindow Window { get; }

    private static void OnCharacter(IKeyboard keyboard, char character) => ImGui.GetIO().AddInputCharacterUTF16(character);
    private static void OnScroll(IMouse mouse, ScrollWheel scroll) => ImGui.GetIO().AddMouseWheelEvent(scroll.X, scroll.Y);
    private static void OnFocusChanged(bool focused) => ImGui.GetIO().AddFocusEvent(focused);

    public void Update()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        if (!IsInitialized)
            throw new InvalidOperationException("Cannot update input before initialization.");

        var io = ImGui.GetIO();
        if (Input.Mice.Count > 0) {
            var mouse = Input.Mice[0];
            io.AddMousePosEvent(mouse.Position.X, mouse.Position.Y);
            io.AddMouseButtonEvent(0, mouse.IsButtonPressed(MouseButton.Left));
            io.AddMouseButtonEvent(1, mouse.IsButtonPressed(MouseButton.Right));
            io.AddMouseButtonEvent(2, mouse.IsButtonPressed(MouseButton.Middle));
        }

        if (Input.Keyboards.Count == 0) return;

        var keyboard = Input.Keyboards[0];
        foreach (var (key, imguiKey) in EditingKeys)
            io.AddKeyEvent(imguiKey, keyboard.IsKeyPressed(key));

        for (var index = 0; index < 26; index++)
            io.AddKeyEvent(ImGuiKey.A + index, keyboard.IsKeyPressed(Key.A + index));

        io.AddKeyEvent(ImGuiKey.ModCtrl, keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight));
        io.AddKeyEvent(ImGuiKey.ModShift, keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight));
        io.AddKeyEvent(ImGuiKey.ModAlt, keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight));
        io.AddKeyEvent(ImGuiKey.ModSuper, keyboard.IsKeyPressed(Key.SuperLeft) || keyboard.IsKeyPressed(Key.SuperRight));
    }

    private IntPtr ReadClipboard(IntPtr context)
    {
        string text;
        try {
            text = Input.Keyboards.Count > 0 ? Input.Keyboards[0].ClipboardText : string.Empty;
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to read the clipboard");
            text = string.Empty;
        }

        Marshal.FreeCoTaskMem(clipboardText);
        clipboardText = Marshal.StringToCoTaskMemUTF8(text);
        return clipboardText;
    }

    private void WriteClipboard(IntPtr context, IntPtr text)
    {
        try {
            if (Input.Keyboards.Count > 0)
                Input.Keyboards[0].ClipboardText = Marshal.PtrToStringUTF8(text) ?? string.Empty;
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to write the clipboard");
        }
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        Window.FocusChanged -= OnFocusChanged;

        if (input is not null) {
            foreach (var keyboard in input.Keyboards)
                keyboard.KeyChar -= OnCharacter;

            foreach (var mouse in input.Mice) {
                mouse.Scroll -= OnScroll;
                mouse.Cursor.CursorMode = CursorMode.Normal;
            }
        }

        if (clipboardCallbacksAttached) {
            var platform = ImGui.GetPlatformIO();
            platform.Platform_GetClipboardTextFn = IntPtr.Zero;
            platform.Platform_SetClipboardTextFn = IntPtr.Zero;
            clipboardCallbacksAttached = false;
        }

        Marshal.FreeCoTaskMem(clipboardText);
        clipboardText = IntPtr.Zero;
        input?.Dispose();
        input = null;
        IsInitialized = false;
    }
}
