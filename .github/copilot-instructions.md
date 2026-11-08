# Copilot Instructions

## Project Guidelines
- User prefers .NET 10 for this project; do not retarget to .NET 8 or .NET 9.
- For nested OpenRCT2 builds, compile only the base `openrct2` project/target. Do not build the full OpenRCT2 solution or configure multiple OS variants, and do not introduce a `zstd` dependency; verify the selected target before starting native builds.
- Do not add or implement tests unless the user explicitly asks. For this feature, the user will manually verify behavior; limit validation to builds and static checks.
- Execute code implementation requests in Agent mode, not left as a registered plan; the user is using Agent mode rather than Plan mode.
- When the user asks a question or gives feedback, answer or clarify only; do not make workspace changes unless they explicitly ask for implementation or approval.
- For RCT3DViewer integration, use the app's own OpenRCT2 config rather than assuming the main game's settings carry over, as the embedded OpenRCT2 instance is isolated from the main game's config/data location.
- For ride configurations, do not directly reuse another ride's registry; give each ride an independent registry so future changes to one ride do not propagate to another.
- For wooden truss behavior, define it by the support type, not individual ride definitions; every occupied track tile gets a placement.
- For the looping coaster, the entry/exit slope segments should be length-adjustable, extend inward from the existing fixed endpoints, and be used to broaden the bottom of the loop. Keep the main loop curve logic intact and avoid changing the established overall apex height if feasible.
- For HalfLoopUp and HalfLoopDown roll handling, use separate per-piece endpoint rules derived from OpenRCT2 track metadata; do not assume both pieces share a constant 180-degree roll.
- Expect minimal, clean geometry changes; avoid unnecessary code expansion or overengineering. When fixing winding, use the existing project convention directly and avoid duplicate calculations or overly generalized helper signatures.
- When diagnosing material ID mappings, verify the viewer's actual resolver and OpenRCT2 object identifiers including legacy aliases; an identifier listed in DefaultObjects alone does not establish that the viewer mapping will match.
- For MetalStick ride supports, use `TrackSupportPlacementMode.TrackBased`, not `TileBased`.
- For the planned bamboo railings (brown) path builder, omit the normal path surface layer only when a support platform is present. Railings consist of two horizontal bamboo poles and three vertical bamboo poles; supports use rounded bamboo poles.
- For procedural mesh quality, use reusable parameterized geometry generation (e.g. endpoints, radius, radial segments), not hardcoded geometry. Do not interpret this as a request for centralized configuration or global/runtime quality controls.
- When diagnosing inverted track centerline heights, note a roughly 1-world-unit excess across loops, corkscrews, barrel rolls, and other inverted parts. The constant global CenterlineHeightOffset is known and is not relevant to this inversion-specific issue; do not attribute it to that offset. Focus on shared incorrect assumptions across inversion families; do not alter manually tuned individual loop geometry as a substitute for finding the broad cause.
- When diagnosing current RCT3DViewer vehicle placement, treat the reported rotation matching a 90-degree right roll but vehicle translation shifting left as a positional/reference-path handedness issue to verify against native movement samples and local attachment transforms, not as evidence to flip the already-correct visible rotation or blame the constant world-height offset.

## Rendering Guidelines
- Request source-derived terrain direction reasoning rather than guess-and-test; validate rendering hypotheses before making further geometry changes.
- For RCT3DViewer, the long-term rendering target is high-quality meshes with PBR textures and likely tessellation. Plan for hierarchical quadtree spatial chunks and finer sections to support frustum culling and future LOD rather than assuming only coarse flat-terrain chunks.
- Configure rendering path surface bias to default slightly above terrain to avoid z-fighting; expose render configuration consistently with existing renderer/environment-variable patterns.
- Implement frustum-culling in RCT3DViewer using a single 8×8-tile region-size constant; pack terrain/path/water geometry into category-level GPU buffers and retain per-region AABBs/index ranges for visible-range draws. Extract one CPU frustum per frame using the row-vector view-projection and Vulkan 0..w clip-depth convention. Sort visible water regions back-to-front; batch repeated path accessories (currently path lamps) by region/model/material with shared model meshes and instanced transforms. Keep region data suitable for a future quadtree and larger objects; do not consider the quadtree or future ride/support/shop geometry implemented yet.
- For terrain-normal smoothing, constrain interpolation to the configured boundary width; do not average the two tile-triangle normals at diagonal vertices in a way that spreads interpolation across the whole center quad. Use geometry/interpolation boundaries so only the intended band is blended. Ensure terrain mesh normals are continuous within a tile, allowing normal discontinuities only at tile boundaries when the neighboring tile has been raised or lowered; deduplicate/reuse within-tile vertices accordingly.
- For terrain material textures, use three packed maps: base RGBA where RGB is albedo and A is opacity; normal RGBA where R/G encode tangent-space normal X/Y and B is AO; specular RGB where R=roughness, G=metalness, and B=scalar SSS amount (no SSS color).
- When updating a material mapping, preserve unrelated working mappings and edit only the mapping requested. In particular, do not alter the existing wood_red terrain-side mapping when fixing another edge mapping.
- Treat correct winding as a mandatory precondition for every generated triangle: determine and emit indices in the CCW order seen by the renderer after the complete transform and viewport path. In RCT3DViewer, account for the camera X mirror, Vulkan viewport Y flip, and CCW front-face pipeline; derive the order from verified project geometry and validate it against supplied outward normals before emitting the mesh. Never assume object-space CCW is sufficient or defer winding correction until after geometry is created.

## RCT3DViewer Guidelines
- RCT3DViewer render-distance sliders should preview changes in real time for both main rendering and shadows; apply saved values and cancel restores previous settings.
- For RCT3DViewer's debug track paths visualization, use actual line rendering: cyan native vehicle movement paths and orange vehicle-local up markers. Do not add separate tangent markers or procedural track-centerline visualization.

## Structural Guidelines
- For wooden path supports, place horizontal side crossmembers between vertical support sections rather than at each section's midpoint; half-frequency means at every other section boundary, using normal beam thickness.

## Geometry Guidelines
- For wooden path-support geometry, infer the structure from the in-game reference: longitudinal trusses follow the railing-bearing sides, with repeated transverse support frames under the path and diagonal bracing between frames; verify the intended member layout before changing geometry when the reference is ambiguous.
- When interpreting topology diagrams, map and validate the explicit vertex/edge connectivity before claiming understanding; if the graph cannot be reliably inferred, ask for concrete connectivity rather than guess.
- For vegetation assets, `*-north` is the north face and mirrored south face; `*-west` is the west face and mirrored east face. They are two face textures on the same vegetation model, not alternative texture variants. Build one combined vegetation mesh/model using both textures and apply cardinal rotation to the whole model; do not make separate north/west meshes or select one texture based on tree direction.
- When generating new mesh geometry, construct vertex/index order correctly from the outset for the project's verified final-render CCW convention. Do not create the mesh in an assumed order and then add position-carrying vertex records or helper-based winding correction logic to repair it afterward; keep geometry generation direct and minimal.
- For tunnel wall corner geometry, move vertical walls inward with wall-thickness insets; at an outset/convex corner, extend the wall ends outward along the corner direction to close the gap, rather than terminating them at the perpendicular wall's inset plane.

## Uniform Block Organization
- When organizing custom uniform blocks, group fields by their commented type/category (Camera, Temporal, Scene), not by usage. Follow this preference rather than reorganizing fields based on usage.