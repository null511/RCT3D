# RCT3DViewer

RCT3DViewer is an experimental real-time 3D viewer for RollerCoaster Tycoon 1/2 park terrain. It uses OpenRCT2 to load park files and extract map surface data, then renders the terrain with Vulkan.

## Repository layout

- `RCT3DViewer/` — .NET 10 application, Vulkan renderer, camera, terrain materials, shaders, and managed OpenRCT2 adapter bindings.
- `ShaderCompiler/` — build-time Slang shader compiler utility.
- `OpenRCT2/` — OpenRCT2 Git submodule, including the RCT3DViewer native adapter sources and the adapter binary expected by the application.

## Requirements

- .NET 10 SDK.
- Git with submodule support.
- Vulkan-capable graphics hardware and a Vulkan runtime/driver.
- The OpenRCT2 adapter DLL at `OpenRCT2\bin\x64\libopenrct2.dll` for the currently configured Windows build. The application project copies it to the application output as `libopenrct2.dll`.
- Valid OpenRCT2, RollerCoaster Tycoon 1, and RollerCoaster Tycoon 2 data directories, plus a park file supported by OpenRCT2, to enter the Park screen. Menu does not require game data or native initialization.

The renderer uses Silk.NET Vulkan and Slang shaders. During the application build, `ShaderCompiler` compiles `.slang` sources to SPIR-V and the project copies terrain textures and OpenRCT2 runtime data into the output directory.

## Get the source

Clone the repository and initialize its submodules:

```powershell
git clone --recurse-submodules https://github.com/null511/RCT3D.git
cd RCT3D
git submodule update --init --recursive
```

If the repository is already cloned, run `git submodule update --init --recursive` from its root.

## Configure data paths

The application starts in **Menu**, with Load Park, Graphics Options, Configuration, and Exit. Configuration has editable `RCT1_PATH`, `RCT2_PATH`, and `OPENRCT2_DATA` fields, each with a folder picker. Apply saves the paths for the next park load; Cancel or closing the window discards edits. Empty fields can be saved, but loading requires all three directories. Errors are displayed without closing Menu.

Paths are saved per user in `%LOCALAPPDATA%\RCT3DViewer\game-paths.json` (the platform's local application data directory). Each path resolves independently in this order: **environment override > saved settings**. Configuration shows the effective source and warns when an environment override supersedes an edited saved value. Empty environment overrides remain invalid rather than falling back to saved paths. Relative data paths resolve from the application output directory. Paths are explicit settings of the embedded viewer instance, not inherited from the main game's OpenRCT2 configuration.

The entry point accepts these environment variables:

| Variable | Purpose |
| --- | --- |
| `RCT3D_OPENRCT2_DLL` | Full path to the OpenRCT2 adapter DLL. Defaults to `libopenrct2.dll` in the application output directory. |
| `RCT3D_OPENRCT2_DATA` | OpenRCT2 data directory, including its `object` subdirectory. |
| `RCT3D_RCT1_PATH` | RollerCoaster Tycoon 1 installation directory. |
| `RCT3D_RCT2_PATH` | RollerCoaster Tycoon 2 installation directory. |
| `RCT3D_PARK` | Park/save file to load automatically after the initial Menu frame. |

For example, set paths in PowerShell before launching:

```powershell
$env:RCT3D_OPENRCT2_DATA = 'D:\RCT3DViewer\RCT3DViewer\bin\Release\net10.0\openrct2-data'
$env:RCT3D_RCT1_PATH = 'D:\Games\RollerCoaster Tycoon Deluxe'
$env:RCT3D_RCT2_PATH = 'D:\Games\RollerCoaster Tycoon 2'
$env:RCT3D_PARK = 'D:\Parks\MyPark.park'
```

Startup reads `RCT3D_` environment variables; `config.ini` is no longer loaded. Graphics Options are shared between Menu and Park and remain persisted separately in `graphics-options.json`.

Park loading displays progress in Menu and switches to **Park** only after successful scene upload. **Park > Return to Menu** fully unloads simulation and park GPU resources while keeping the window and device alive. A configured startup park is loaded only once, not every time you return to Menu. Loading another park serializes teardown and initialization; failures return to Menu for configuration or retry. Exit during loading waits for owned work to finish safely.

## Infrastructure ownership

`Program.cs` is the DI composition root. App-lifetime settings, UI, window, Vulkan resources, input, and frame coordination are constructor-injected singletons. Managed construction does not initialize Vulkan or input: the window load event initializes Vulkan/ImGui first, then input. Screens submit typed navigation commands that Renderer processes before image acquisition, so they never depend on Renderer itself.

The container owns shared-service disposal; Renderer owns only its event subscriptions and pending/active parks. `ParkScreenFactory` prepares native/CPU startup on background work and constructs the explicitly owned park graph on the render thread. Shutdown disposes parks before shared input, Vulkan/ImGui, and the window. Per-park objects are not registered as root-container transients or retained until application exit.

## Build and run

From the repository root in a Visual Studio Developer PowerShell:

```powershell
MSBuild .\RCT3DViewer\RCT3D-Viewer.csproj /restore /p:Configuration=Release
dotnet run --project .\RCT3DViewer\RCT3D-Viewer.csproj --configuration Release --no-build
```

The application build compiles Slang shaders, builds only the base OpenRCT2 `libopenrct2.vcxproj` target, and copies the adapter, runtime data, and terrain textures to the application output directory. It does not build the full OpenRCT2 solution. The DLL must include the viewer adapter's ABI 6 exports. Alternatively, set `OpenRCT2AdapterDll` to an ABI-compatible adapter DLL path:

```powershell
MSBuild .\RCT3DViewer\RCT3D-Viewer.csproj /restore /p:Configuration=Release /p:OpenRCT2AdapterDll="D:\path\to\libopenrct2.dll"
```

## Controls

- Hold the right mouse button and move the mouse to look around.
- `W`, `A`, `S`, `D` to fly.
- `Space` to move up; `Left Ctrl` to move down.
- `F9` toggles simulation pause; `F3` toggles the debug overlay.
- `P` toggles Peep Mode; `E` interacts with a ride entrance in Peep Mode.
- Use **Park > Return to Menu** to unload the park. Park controls are inactive in Menu and while typing in UI fields.

## Current scope

The viewer currently focuses on loading a park through the OpenRCT2 adapter and rendering its terrain with terrain-surface materials. It is an active development project; other park elements and a polished cross-platform packaging workflow are not yet documented as supported. The project file currently packages a Windows `.dll`, so Linux/macOS native packaging requires additional configuration even though the renderer is based on Vulkan and Silk.NET.
