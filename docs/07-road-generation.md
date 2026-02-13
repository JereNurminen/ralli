# 7. Road Generation (Runtime)

## 7.1 Output
For each generated chunk:
- Centerline samples (distance-along-road **s**)
- Extruded mesh using a **RoadProfile** (cross-section samples)
- MeshCollider (optionally simplified later)
- Vertex colors painted from season/weather rules

## 7.2 Determinism
- All generation driven by **seed**.
- Road is built as a stream of parametric segments, sampled into a polyline.

## 7.3 Chunking + Seams
- Fixed chunk length in meters (e.g., 120–160 m).
- **Seam-safe sampling:** chunk N includes end sample; chunk N+1 excludes its start sample so vertices match exactly.
- Culling/generation based on **player distance s**, not world Z.
- Pool chunks to avoid allocations.

## 7.4 Frames + Banking
- Use **parallel transport frames** for stable right/up vectors over hills.
- Banking derived from curvature proxy (tangent delta / ds), clamped.
