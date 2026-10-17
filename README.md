# RCT3DViewer

RCT3DViewer is an experimental real-time 3D viewer for RollerCoaster Tycoon 1/2 park terrain. It uses OpenRCT2 to load park files and extract map surface data, then renders the terrain with Vulkan.

## Repository layout

- `RCT3DViewer/` — .NET 10 application, Vulkan renderer, camera, terrain materials, shaders, and managed OpenRCT2 adapter bindings.
- `ShaderCompiler/` — build-time Slang shader compiler utility.
- `OpenRCT2/` — OpenRCT2 Git submodule, including the RCT3DViewer native adapter sources and the adapter binary expected by the application.

## Requirements

- .NET 10 SDK.
- Vulkan-capable graphics hardware and a Vulkan runtime/driver.
- Git with submodule support.
- The OpenRCT2 adapter DLL at `OpenRCT2/bin-adapter/libopenrct2.dll` for the currently configured Windows build. The application project copies it to the application output as `libopenrct2.dll`.
- Valid OpenRCT2 and RollerCoaster Tycoon 2 data directories, plus a park file supported by OpenRCT2.

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

The current entry point accepts these environment variables to override its configured defaults:

| Variable | Purpose |
| --- | --- |
| `RCT3DVIEWER_OPENRCT2_DLL` | Full path to the OpenRCT2 adapter DLL. Defaults to `libopenrct2.dll` in the application output directory. |
| `RCT3DVIEWER_OPENRCT2_DATA` | OpenRCT2 data directory. A relative path is resolved from the application output directory; the default is `openrct2-data`. |
| `RCT3DVIEWER_RCT2_DATA` | RollerCoaster Tycoon 2 data directory. |
| `RCT3DVIEWER_PARK` | Park/save file to load. |

For example, set paths in PowerShell before launching:

```powershell
$env:RCT3DVIEWER_OPENRCT2_DLL = 'D:\RCT3DViewer\OpenRCT2\bin-adapter\libopenrct2.dll'
$env:RCT3DVIEWER_OPENRCT2_DATA = 'D:\RCT3DViewer\RCT3DViewer\bin\Debug\net10.0\openrct2-data'
$env:RCT3DVIEWER_RCT2_DATA = 'D:\Games\RollerCoaster Tycoon 2'
$env:RCT3DVIEWER_PARK = 'D:\Parks\MyPark.park'
```

The application currently has developer-machine defaults for the RCT2 data directory and park path in `RCT3DViewer/Program.cs`; use the environment variables above when those defaults do not exist on your machine.

## Build and run

From the repository root:

```powershell
dotnet build RCT3DViewer.slnx
dotnet run --project RCT3DViewer/RCT3DViewer.csproj
```

The build compiles the Slang shaders and copies the OpenRCT2 adapter, runtime data, and terrain textures to the application output directory. If the adapter binary is missing, build the OpenRCT2 submodule's `rct3dviewer` adapter target using that submodule's build instructions, or set `OpenRCT2AdapterDll` to the built DLL path when building:

```powershell
dotnet build RCT3DViewer.slnx -p:OpenRCT2AdapterDll="D:\path\to\libopenrct2.dll"
```

## Controls

- Hold the right mouse button and move the mouse to look around.
- `W`, `A`, `S`, `D` to fly.
- `Space` to move up; `Left Ctrl` to move down.

## Current scope

The viewer currently focuses on loading a park through the OpenRCT2 adapter and rendering its terrain with terrain-surface materials. It is an active development project; other park elements and a polished cross-platform packaging workflow are not yet documented as supported. The project file currently packages a Windows `.dll`, so Linux/macOS native packaging requires additional configuration even though the renderer is based on Vulkan and Silk.NET.
