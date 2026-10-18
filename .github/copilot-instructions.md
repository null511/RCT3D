# Copilot Instructions

## Project Guidelines
- User prefers .NET 10 for this project; do not retarget to .NET 8 or .NET 9.
- For build issues specifically in the nested OpenRCT2 project/repository, do not use the MSBuild agent; it repeatedly targets the RCT3DViewer project instead. Diagnose the OpenRCT2 project directly or use another suitable approach.
- Do not add or implement tests unless the user explicitly asks. For this feature, the user will manually verify behavior; limit validation to builds and static checks.

## Rendering Guidelines
- Request source-derived terrain direction reasoning rather than guess-and-test; validate rendering hypotheses before making further geometry changes.
- For RCT3DViewer, the long-term rendering target is high-quality meshes with PBR textures and likely tessellation. Plan for hierarchical quadtree spatial chunks and finer sections to support frustum culling and future LOD rather than assuming only coarse flat-terrain chunks.
- Configure rendering path surface bias to default slightly above terrain to avoid z-fighting; expose render configuration consistently with existing renderer/environment-variable patterns.