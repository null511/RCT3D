# Copilot Instructions

## Project Guidelines
- User prefers .NET 10 for this project; do not retarget to .NET 8 or .NET 9.
- For nested OpenRCT2 builds, compile only the base `openrct2` project/target. Do not build the full OpenRCT2 solution or configure multiple OS variants, and do not introduce a `zstd` dependency; verify the selected target before starting native builds.
- Do not add or implement tests unless the user explicitly asks. For this feature, the user will manually verify behavior; limit validation to builds and static checks.

## Rendering Guidelines
- Request source-derived terrain direction reasoning rather than guess-and-test; validate rendering hypotheses before making further geometry changes.
- For RCT3DViewer, the long-term rendering target is high-quality meshes with PBR textures and likely tessellation. Plan for hierarchical quadtree spatial chunks and finer sections to support frustum culling and future LOD rather than assuming only coarse flat-terrain chunks. Suppress tessellation where paths exist; paths are not intended to follow displaced terrain. Account for displacement transitions at path edges.
- Configure rendering path surface bias to default slightly above terrain to avoid z-fighting; expose render configuration consistently with existing renderer/environment-variable patterns.