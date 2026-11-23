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

## Ride vehicle models

Each car in `RCT3DViewer\Rides\Vehicles\*.json` can optionally specify a static glTF/GLB model:

```json
{
  "cars": [
	{
	  "model": "canoe.glb",
	  "modelScale": 1.0,
	  "size": [0.4, 0.24, 2.0],
	  "localOffset": -0.02
	}
  ]
}
```

Model paths resolve relative to the application's `Assets\Models\Vehicle` directory, not the current working directory or the user's home. Subdirectories are allowed; absolute paths and paths outside that directory are rejected. Only vehicle types needed by the current park load models, with one shared model reference per type; there is no startup preloading or separate asset cache. Newly appearing vehicle types load on demand.

`modelScale` defaults to `1.0`, must be finite and positive, and is baked into mesh vertices and bounds once at load time. Models retain their authored origin and axes (Y up, X width, Z length), without automatic centering or height fitting. Existing `localOffset`, `worldOffset`, and `cameraOffset` retain their meanings.

Missing or null `model` uses the existing cube. A configured model that cannot load logs an error once per type per park and also uses the cube. `size` controls only that cube and is baked into its shared geometry; it does not resize imported models. Both model and cube instances carry only pose and dynamic primary vehicle color. The shader multiplies authored base-color RGB by the linearized per-instance color, preserving textures and opacity. Geometry/materials are shared by differently colored vehicles of the same type, and park-specific resources are released on unload/replacement.

## Mine-car reference sprites

AssetExtractor can export four empty, upright Mine Cars views for the wooden wild mouse (`rct2.ride.wmmine`). Configure `GAME_PATH` and `OPENRCT2_BIN` in `AssetExtractor\configuration.py`: it requires your own game's `ObjData\WMMINE.DAT`, the OpenRCT2 executable, and its installed `data\object\rct2\ride\rct2.ride.wmmine.json` catalog entry.

Run from the AssetExtractor directory with its configured Python environment and dependencies:

```powershell
Set-Location .\AssetExtractor
.\.venv\Scripts\python.exe main.py --export-vehicle mine-cars
```

The full raw object sprite set and original drawing offsets are saved under `RAW_DIR\Vehicles\rct2.ride.wmmine\WMMINE`. Only four final PNGs are published under `OUTPUT_DIR\vehicles\mine-cars`: `view-00.png`, `view-08.png`, `view-16.png`, and `view-24.png`. These are quarter-turn isometric views of car type 0 on flat track with neutral swing, not orthographic views. Pixels, dimensions, transparency, and the original exported palette are preserved without colour remapping or passenger overlays. `reference_views.json` records the source indices (3, 27, 51, 75) and x/y drawing offsets.

Output paths follow `configuration.py` and resolve relative to the working directory when configured as relative paths. Reruns refresh the raw export and replace only the named final outputs. Missing files, incompatible metadata, or export failures report an error and return a nonzero exit code. Running without `--export-vehicle` keeps the existing extraction/processing workflow; the vehicle command does not process vegetation or deploy assets into the viewer.

## Controls

- Hold the right mouse button and move the mouse to look around.
- `W`, `A`, `S`, `D` to fly.
- `Space` to move up; `Left Ctrl` to move down.
- `F9` toggles simulation pause; `F3` toggles the debug overlay.
- `P` toggles Peep Mode; `E` interacts with a ride entrance in Peep Mode.
- Use **Park > Return to Menu** to unload the park. Park controls are inactive in Menu and while typing in UI fields.

## Current scope

The viewer currently focuses on loading a park through the OpenRCT2 adapter and rendering its terrain with terrain-surface materials. It is an active development project; other park elements and a polished cross-platform packaging workflow are not yet documented as supported. The project file currently packages a Windows `.dll`, so Linux/macOS native packaging requires additional configuration even though the renderer is based on Vulkan and Silk.NET.
