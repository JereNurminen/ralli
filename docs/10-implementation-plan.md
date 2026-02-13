# 10. Implementation Plan (Engineering)

## Phase A — Vehicle Physics (RWD-first, flat plane)
1. Rigidbody + raycast wheel suspension (spring + damper).
2. Slip-based tire forces + friction model (μ scaling).
3. Combined friction ellipse clamp (braking + cornering share grip budget).
4. Rear LSD (power lock) + AWD torque split support (rear-biased default).
5. Stability aids as toggles/curves (yaw damping, anti-roll, assist).
6. ScriptableObject configs: CarHandling, Tire, Drivetrain.
7. Test scene: flat plane, basic input binding, follow camera.

**Goal:** the car must feel fun to drive on a flat plane before moving on.

## Phase B — Surfaces + Road Generation
1. SurfaceResolver: raycast hit → barycentric vertex color → μ.
2. Surface friction integration with tire force pipeline.
3. RoadStream seeded segment generator (segment params → centerline samples).
4. RoadProfile extrusion → Mesh + MeshCollider.
5. Seam-safe chunking + pooling; generate/cull by player distance **s**.
6. Surface painting (season/weather rules) → vertex colors.

## Phase C — Gameplay Loop
1. Time buffer + threat pacing.
2. Scoring + multiplier.
3. Traffic spawner + near-miss scoring.
4. Meta progression between runs.

## Phase D — Post-MVP
- Service stops (roguelike parts).
- More surface rules (ice patches, wetness bands).
- More car archetypes / drivetrains after RWD is proven.
