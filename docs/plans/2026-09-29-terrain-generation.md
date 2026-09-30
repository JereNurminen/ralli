# Terrain Generation Rework Plan

**Spec:** `docs/2026-09-plan/01-terrain-generation.md`
**Goal:** Stop the road's forest skirt from folding over itself on curves. To do that, shrink the spline-extruded mesh to a narrow road corridor and put everything else on a world-space heightfield that adapts to the road.
**Verification:** compile check, then playtest. No unit tests (existing road tests are kept compiling).

## Decisions (simplifications vs. spec)

1. **Cut-and-fill is a height blend, not extra geometry.** Terrain height at any point = `lerp(road lip height, H(x,z), smoothstep(distance-to-road over roadBlendWidth))`. Cut and fill fall out of the blend automatically, with a fixed lateral extent instead of a fixed face slope. It is one mechanism for procedural and designed stretches (1.3 + 1.4).
2. **Curvature ceiling enforced at runtime, in one place.** The sample generator clamps turn rate so the corridor never folds (min radius = corridor half-width × `corridorRadiusMargin`). This covers procedural and designed pieces alike, so bake-time clamping is not needed. `AdjustLateralForCurvature` is deleted.
3. **Road elevation follows the terrain.** Procedural road target height = `H` along the road (grade-limited, absolute tracking), so cut/fill stays small. Designed pieces keep their authored shape (relative), and the offset they leave behind decays back to `H`. The old sine-wave `largeHill*` relief is removed, while the short bumps stay.
4. **Trees live on terrain tiles.** A jittered world grid is kept only inside a band next to the road, with a capsule collider per tree. Tree lifetime follows the terrain tile (the spec says road chunk; the tile is the equivalent streaming unit now).
5. **Terrain tiles rebuild if new road appears in their area** (rare: the road looping back later).
6. **Skipped:** the ring-order debug assert (the clamp guarantees it), per-piece cut/fill amount, and the contour-following router.

## Steps

1. Config: add Terrain + Trees fields; drop skirt/forest-strip/compression/large-hill fields.
2. `TerrainHeightField` (new): seeded fBm noise `H(x,z)`, normalized to the road origin.
3. `RoadStreamGenerator`: narrow profile + short apron, turn-rate ceiling, sample spatial index + `TryGetRoadProximity`, elevation from `H`, directional bias for procedural curves, remove tree code.
4. `TerrainStreamer` (new): tile streaming around the car (biased toward target bearing), ground mesh + collider, trees, dirty-tile rebuild.
5. Scene: add `TerrainStreamer` to `RoadStream` and move the tree prefab/material references onto it.
