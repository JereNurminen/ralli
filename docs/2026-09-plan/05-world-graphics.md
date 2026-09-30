# Phase 5 — Terrain and world graphics

Depends on: Phase 1. Must not require changes to Phase 1 geometry; if it does, Phase 1 was not fully decoupled from appearance.

## Model

The forest is the terrain; the road is a cut through a forest blanket. Three visual layers:

1. **Ditch wall** (seen from the road).
2. **Near-road trees** (billboard decals with real colliders from 1.6).
3. **Canopy top** (seen from hills/altitude): near/mid textured heightfield plus a camera-relative far disc.

## 5.1 Ditch wall

- [ ] Texture the Phase 1.3 cut/fill face (tiled or triplanar: soil, roots, moss, occasional boulder). Geometry is unchanged.

## 5.2 Near-road trees

- [ ] Render trees as billboard/cutout decal cards. Collision stays as in 1.6 (capsule/cylinder, independent of the card).
- [ ] Bias placement density high near the road edge for strong whip-by motion cues.

## 5.3 Canopy top

- [ ] **Near/mid:** texture the Phase 1.2 heightfield as canopy: mottled green/color noise plus small vertex-shader bump displacement.
- [ ] **Far field:** one large camera-relative ground disc (follows camera translation, never rotates) with the same canopy-noise shader, color-faded to fog/sky at its outer edge. Replaces tile streaming beyond the near/mid range; needs no geometric correlation with real terrain.
- [ ] Seam between heightfield and disc needs only fog/color continuity.
- [ ] No collision on the canopy layer.
- [ ] Heavy near fog; keep tree fill dense on slopes to hide LOD transitions and the corridor/heightfield seam.

## 5.4 Shading and biomes

- [ ] Matte, low-gloss shading. Ground blending via vertex color or per-pixel shader mask keyed to distance-to-spline (asphalt/dirt/forest/meadow); no texture splatting.
- [ ] Biome enum per chunk/span (Forest, Meadow, Field) with Markov transition (~90% stay / 10% switch) so biomes persist across chunks.
- [ ] Parameterize the 1.6 placement mechanism by biome (prefab set + density: grass clumps, hay bales, fence posts, rocks, or none). Vary canopy noise parameters so a meadow reads as a break in the canopy from above.
- [ ] Biome changes are decoration/texture only; never alter corridor or cut/fill geometry.
