RCT3DViewer - External Native Dependencies & Setup
===============================================

This document lists the native prerequisites and recommended setup steps to get the RCT3DViewer running across platforms. The managed code scaffolds a simulation bridge (libopenrct2), a Slang shader compile step and a Vulkan renderer; the native artifacts must be provided separately.

Required native components
- libopenrct2 (custom fork with small C-compatible wrapper exports)
  - Provide platform builds: libopenrct2.dll (Windows), libopenrct2.so (Linux), libopenrct2.dylib (macOS)
  - Place binaries into the application output folder (e.g. bin/Debug/net9.0/) or a system search path.

- Slang runtime / slangc
  - The project includes a ShaderCompiler tool that uses Slangc.NET to compile Slang sources at build time. Ensure Slangc runtime is available to Slangc.NET according to its docs.

- Vulkan loader and ICD
  - Windows: install the latest GPU driver providing Vulkan support.
  - macOS: install MoltenVK and ensure a MoltenVK dylib is discoverable by the app or installed system-wide. Alternatively use LunarG SDK.

Runtime loader behavior
- The managed NativeLoader will search the application's base directory for candidate file names (libopenrct2.dll/.so/.dylib) and then fall back to the system loader.

Development notes
- The project currently targets .NET 10 in the repository. For strict compatibility with the Core Project Goal you may prefer to retarget to net9.0 or net8.0; this is optional but recommended for parity with some native interop toolchains.
- The SimulationBridge exposes a FillSurfaceData(Span<T>) method so callers can reuse pinned buffers and avoid allocations during per-frame updates.
- The ShaderCompiler project compiles Slang shaders at build time into SPIR-V blobs saved into the project's output Shaders/ folder.

Next steps suggested by plan
- Add a robust native distribution mechanism for libopenrct2, MoltenVK and Slang into CI and release artifacts.
- Implement and centralize the full Vulkan renderer in Render/Renderer.cs with instancing and pinned GPU transform buffers.
- Harden zero-allocation patterns by adding benchmarks and guard rails (Roslyn analyzer or runtime assertions).
