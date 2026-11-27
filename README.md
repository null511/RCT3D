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

The application build compiles Slang shaders, builds only the base OpenRCT2 `libopenrct2.vcxproj` target, and copies the adapter, runtime data, and terrain textures to the application output directory. It does not build the full OpenRCT2 solution. The DLL must include the viewer adapter's ABI 46 exports. Alternatively, set `OpenRCT2AdapterDll` to an ABI-compatible adapter DLL path:

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

## Path accessory models

Place each bin, lamp or bench model at `RCT3DViewer\Assets\Models\Path\<object-id>\model.glb`, for example `Path\rct2.footpath_item.litter1\model.glb`. The folder name must exactly match the loaded OpenRCT2 object's identifier. Models retain their authored scale and origin; Y is up, and the unrotated model is placed at the northwest path edge before rotating for other edges. One world unit is one third of a path tile.

Only matching models are rendered; unmatched accessories are skipped and their identifier hashes are logged. The old lamp and bench manifests and default models are not used as fallbacks. Raw legacy DAT objects can have no modern identifier, so they cannot necessarily match these folders. Unsupported path additions such as fountains are not rendered by this catalog. A directory without `model.glb` or a model that fails to load reports an error.

## Vehicle reference sprites

AssetExtractor exports four primary flat-track directions for every car type defined by a ride object's metadata. It reads metadata from a `.parkobj` file or, with `--vehicle-metadata`, from a separate JSON file for a DAT source. Configure `OPENRCT2_BIN` in `AssetExtractor\configuration.py`.

For example, to extract the Mine Train coaster:

```powershell
Set-Location .\AssetExtractor
.\.venv\Scripts\python.exe main.py --export-vehicle "C:\Program Files\OpenRCT2\data\object\rct1\ride\rct1.ride.mine_trains.parkobj"
```

The full raw sprite set and original drawing offsets are saved under `RAW_DIR\Vehicles\<object-id>\<source-name>`. Primary views and `reference_views.json` are written under `OUTPUT_DIR\vehicles\<object-id>`, with one `car-<type>-view-<direction>.png` per car type and cardinal direction. The selected PNGs are copied unchanged, without color remapping or rider overlays; the sidecar records each source image index and drawing offset.

Output paths follow `configuration.py` and resolve relative to the working directory when configured as relative paths. Reruns replace only the named final outputs. Missing files, incompatible metadata, or export failures report an error and return a nonzero exit code. Running without `--export-vehicle` keeps the existing extraction/processing workflow; the vehicle command does not process vegetation or deploy assets into the viewer.

## Path sprites and textures

From the `AssetExtractor` directory, run:

```powershell
.\.venv\Scripts\python.exe main.py --export-paths
```

Configure `OPENRCT2_BIN` in `configuration.py`. The command discovers `.parkobj` and `.json` footpath surface objects in the installed OpenRCT2 object groups, excluding invisible path and queue surfaces. It exports each complete sprite set to `RAW_DIR\Paths\<object-id>`, with original drawing offsets in `sprite_offsets.json`. Raw exports include preview and slope sprites. OpenRCT2 resolves referenced game sprites using its own data configuration and may use an object's RCT2 fallback images when RCT1 CSG data is unavailable; the appropriate game data must be configured in OpenRCT2.

Square 32x32 RGBA textures are written to `OUTPUT_DIR\path\<object-id>`. Ordinary paths export the 47 flat indices `01`–`16` and `21`–`51`; queues export 11 flat variants, excluding three- and four-way junction indices `08`, `12`, `14`, `15`, and `16`. The raw export still contains all source sprites. Original offsets align cropped sprites to the same tile footprint; preview and slope sprites are not converted. The conversion uses OpenRCT2's 1:2 projection, then aligns zero-based odd output columns one pixel downward, repeating the top sample and clipping the bottom pixel. Each object's textures are staged before replacement; previously exported excluded files are not automatically deleted. Errors are reported, other objects are still attempted, and any failure returns a nonzero exit code. Re-export to apply conversion changes to older outputs. Textures are not deployed to the viewer.

Copy the desired object directories to `RCT3DViewer\Assets\Textures\path`. The viewer selects the flat sprite index from the path's connections and corners, including on slopes, and loads only variants used by the park's visible path surfaces. Each base `NN.png` is RGBA albedo and opacity; optional 32x32 `NN_n.png` maps store normal X/Y in R/G and ambient occlusion in B, and `NN_s.png` maps store roughness, metalness and scalar subsurface scattering in R/G/B. Missing companion maps use solid defaults. Missing surface variants and unsupported queue junctions use `path\missing.png` and are logged. Surface opacity is cut out in the visible and shadow passes; platform slabs remain textured, while path supports use builder-supplied solid colors.

## Terrain surface textures

From the `AssetExtractor` directory, run:

```powershell
.\.venv\Scripts\python.exe main.py --export-terrain
```

Configure `GAME_PATH` for RCT2, `RCT1_PATH` for RCT1 Loopy Landscapes, and `OPENRCT2_BIN` for the installed OpenRCT2 executable in `configuration.py`. The command extracts only the required sprites from `GAME_PATH\Data\g1.dat`, RCT1's CSG files, and OpenRCT2's bundled terrain surface objects. It does not require `--extract-all` or pre-exported G1 PNGs. RCT1 requires `Data\CSG1I.DAT` and `Data\CSG1.DAT` (or `CSG1.1`), with the alternate index location `RCTdeluxe_install\Data\CSG1I.DAT` also supported. RCT1 surface objects and the official wildflowers object must be installed with OpenRCT2. Incompatible object layouts are reported rather than silently using fallback images.

Outputs are 32x32 PNGs in `OUTPUT_DIR\terrain`, including the existing RCT2 and grass textures and `roof_red`, `roof_grey`, `rust`, `wood`, and `wildflowers`. The viewer includes mappings for these five non-recolourable official surfaces. Recolourable grid, designer grid, and void surfaces remain unsupported. Exports are staged and replace only named outputs; independent assets are attempted after errors, and a partial failure returns a nonzero exit code. Paths follow `configuration.py` and resolve relative to the working directory when configured as relative paths. The command does not deploy outputs to the viewer; copy intended new textures into `RCT3DViewer\Assets\Textures\terrain` when updating packaged assets.

## Terrain side textures

From the `AssetExtractor` directory, run:

```powershell
.\.venv\Scripts\python.exe main.py --export-terrain-sides
```

Configure `RCT1_PATH` in `AssetExtractor\configuration.py` to a RollerCoaster Tycoon 1 Loopy Landscapes installation and `OPENRCT2_BIN` to the installed OpenRCT2 executable. RCT1's `Data\CSG1I.DAT` and `Data\CSG1.DAT` (or `CSG1.1`), OpenRCT2's terrain edge objects under `data\object\rct1\terrain_edge`, and its `data\palettes.dat` are required for the additional sides. The command extracts only the needed RCT1 sprites into temporary files; it never substitutes RCT2 fallback sprites. The existing RCT2 sides still require previously extracted 32x31 sprites under `RAW_DIR\g1`: `sprite_1594.png` (rock), `sprite_1668.png` (wood_black), `sprite_1752.png` (wood_red), and `sprite_1836.png` (ice). Paths follow `configuration.py` and resolve relative to the working directory when configured as relative paths.

Each sprite is straightened into a 32x16 strip by moving the source window upward one pixel every two columns, then repeated vertically to produce a 32x32 RGBA texture without resampling. Outputs under `OUTPUT_DIR\terrain_side` are `rock.png`, `wood_black.png`, `wood_red.png`, `ice.png`, `brick.png`, `iron.png`, `grey.png`, `yellow.png`, `red.png`, `purple.png`, `green.png`, `stone_brown.png`, `stone_grey.png`, `skyscraper_a.png`, and `skyscraper_b.png`. `stone_grey.png` bakes OpenRCT2's default grey palette; it does not represent per-tile colour changes.

Reruns replace the named outputs; RCT1 outputs are staged before replacement. Missing/corrupt data, unsupported layouts, and output errors are reported and return a nonzero exit code; independent assets are still attempted. All RCT1 inputs are required for complete success. The command does not extract the existing RCT2 sprites or deploy textures into the viewer.

## Plain ride entrance and exit sprites

With `OPENRCT2_BIN` configured, run from the AssetExtractor directory:

```powershell
.\.venv\Scripts\python.exe main.py --export-station plain
```

This requires the installed OpenRCT2 `data\object\rct2\station\rct2.station.plain.parkobj` and an RCT2 installation configured in OpenRCT2 so it can resolve the object's `g1.dat` images. It exports eight PNGs to `OUTPUT_DIR\station\plain`: `entrance-0.png` through `entrance-3.png` and `exit-0.png` through `exit-3.png`. Each combines the front and back layers at their original drawing offsets. `sprite_offsets.json` records composite offsets and source indices and offsets. Colours are not remapped; scrolling ride-name text and supports are drawn separately by OpenRCT2. Reruns replace only the named outputs. Missing data, incompatible object metadata, or export failures return a nonzero exit code.

## Controls

- Hold the right mouse button and move the mouse to look around.
- `W`, `A`, `S`, `D` to fly.
- `Space` to move up; `Left Ctrl` to move down.
- `F9` toggles simulation pause; `F3` toggles the debug overlay.
- `P` toggles Peep Mode; `E` interacts with a ride entrance in Peep Mode.
- While riding, look is limited to 90° left or right and 89° up or down relative to the vehicle.
- Use **Park > Return to Menu** to unload the park. Park controls are inactive in Menu and while typing in UI fields.

## Current scope

The viewer currently focuses on loading a park through the OpenRCT2 adapter and rendering its terrain with terrain-surface materials. It is an active development project; other park elements and a polished cross-platform packaging workflow are not yet documented as supported. The project file currently packages a Windows `.dll`, so Linux/macOS native packaging requires additional configuration even though the renderer is based on Vulkan and Silk.NET.
