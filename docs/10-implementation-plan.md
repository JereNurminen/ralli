# 10. Implementation Plan (Engineering)

## Phase A — Road + Surfaces
1. RoadStream seeded segment generator (segment params → centerline samples).
2. RoadProfile extrusion → Mesh + MeshCollider.
3. Seam-safe chunking + pooling; generate/cull by player **s**.
4. Surface painting (season/weather) → vertex colors.
5. SurfaceResolver: raycast hit → barycentric color sample → μ.

## Phase B — Car Physics (RWD-first)
1. Rigidbody + wheel raycast suspension.
2. Slip-based tire forces + μ scaling.
3. Combined friction ellipse clamp.
4. Rear LSD (power lock) + AWD torque split support (rear-biased default).
5. Stability aids as toggles/curves (yaw damping, anti-roll, assist).

## Phase C — Gameplay Loop
1. Time buffer + threat pacing.
2. Scoring + multiplier.
3. Traffic spawner + near-miss scoring.
4. Meta progression between runs.

## Phase D — Post-MVP
- Service stops (roguelike parts)
- More surface rules (ice patches, wetness bands)
- More car archetypes / drivetrains after RWD is proven
