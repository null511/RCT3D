# Copilot Instructions

## Project Guidelines
- User prefers .NET 10 for this project; do not retarget to .NET 8 or .NET 9.
- For nested OpenRCT2 builds, compile only the base `openrct2` project/target. Do not build the full OpenRCT2 solution or configure multiple OS variants, and do not introduce a `zstd` dependency; verify the selected target before starting native builds.
- Do not add or implement tests unless the user explicitly asks. For this feature, the user will manually verify behavior; limit validation to builds and static checks.
- Execute code implementation requests in Agent mode, not left as a registered plan; the user is using Agent mode rather than Plan mode.
- For RCT3DViewer integration, use the app's own OpenRCT2 config rather than assuming the main game's settings carry over, as the embedded OpenRCT2 instance is isolated from the main game's config/data location.
- When implementing a requested fix, verify the actual workspace diff and report concrete files/changes; do not claim the issue is fixed based only on a small speculative code edit and successful compilation.

## Rendering Guidelines
- Request source-derived terrain direction reasoning rather than guess-and-test; validate rendering hypotheses before making further geometry changes.
- For RCT3DViewer, the long-term rendering target is high-quality meshes with PBR textures and likely tessellation. Plan for hierarchical quadtree spatial chunks and finer sections to support frustum culling and future LOD rather than assuming only coarse flat-terrain chunks.
- Configure rendering path surface bias to default slightly above terrain to avoid z-fighting; expose render configuration consistently with existing renderer/environment-variable patterns.
- Implement frustum-culling in RCT3DViewer using a single 8×8-tile region-size constant; pack terrain/path/water geometry into category-level GPU buffers and retain per-region AABBs/index ranges for visible-range draws. Extract one CPU frustum per frame using the row-vector view-projection and Vulkan 0..w clip-depth convention. Sort visible water regions back-to-front; batch repeated path accessories (currently path lamps) by region/model/material with shared model meshes and instanced transforms. Keep region data suitable for a future quadtree and larger objects; do not consider the quadtree or future ride/support/shop geometry implemented yet.
- For terrain-normal smoothing, constrain interpolation to the configured boundary width; do not average the two tile-triangle normals at diagonal vertices in a way that spreads interpolation across the whole center quad. Use geometry/interpolation boundaries so only the intended band is blended. Ensure terrain mesh normals are continuous within a tile, allowing normal discontinuities only at tile boundaries when the neighboring tile has been raised or lowered; deduplicate/reuse within-tile vertices accordingly.
- For terrain material textures, use three packed maps: base RGBA where RGB is albedo and A is opacity; normal RGBA where R/G encode tangent-space normal X/Y and B is AO; specular RGB where R=roughness, G=metalness, and B=scalar SSS amount (no SSS color).

## Structural Guidelines
- For wooden path supports, place horizontal side crossmembers between vertical support sections rather than at each section's midpoint; half-frequency means at every other section boundary, using normal beam thickness.

## Geometry Guidelines
- For wooden path-support geometry, infer the structure from the in-game reference: longitudinal trusses follow the railing-bearing sides, with repeated transverse support frames under the path and diagonal bracing between frames; verify the intended member layout before changing geometry when the reference is ambiguous.
- When interpreting topology diagrams, map and validate the explicit vertex/edge connectivity before claiming understanding; if the graph cannot be reliably inferred, ask for concrete connectivity rather than guess.

## Uniform Block Organization
- When organizing custom uniform blocks, group fields by their commented type/category (Camera, Temporal, Scene), not by usage. Follow this preference rather than reorganizing fields based on usage.