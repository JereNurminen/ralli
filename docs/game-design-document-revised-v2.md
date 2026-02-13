# Arcade Driving Roguelite — Design + Implementation Spec (Token-Optimized)

> **Primary goal:** nail **RWD** handling + drift-driven scoring on a procedurally generated road through a single **Nordic forest** setting (varied via **season**, **day/night**, **weather**).  
> **Secondary goal:** keep systems **data-driven and configurable** (ScriptableObjects), so balancing is iteration-friendly.

---

## 0. Global Specs

- **Engine:** Unity
- **Units:** Metric only  
  - **1 Unity world unit = 1 meter**
  - Speed: **m/s** (UI may also show **km/h**)
  - Angles: degrees
- **Input:** Unity **New Input System**
- **Configurability:** Prefer ScriptableObjects for:
  - Car handling (per drivetrain, per axle, per tire)
  - Surface friction/effects
  - Road profile + generation params
  - Difficulty/scaling curves (threat speed, scoring multipliers, traffic density)

---

## 1. Core Loop

- Single-player, endless/procedural road.
- Run ends on crash (permadeath).
- Earn **style points** during run.
- Spend style points **between runs** on persistent unlocks/starting loadout.

**MVP:** no pit/service stops, no stores; one continuous run.

---

## 2. Player Pressure: Time Buffer (“The Chase”)

- Player has a **time buffer** ahead of an unseen threat.
- Buffer increases when the player gains time (speed + performance); decreases when slowing/crashing/choosing time-cost actions.
- Threat baseline speed is constant; buffer change depends on player speed relative to baseline.
- Soft cap: additional time gained beyond a window has diminishing returns (prevents permanent safety).

**Design intent:** runs can enter a “flow” state but must remain risky; avoid a permanent runaway.

---

## 3. Scoring (Style Points)

- Style points earned from:
  - Drift angle + duration (RWD focus)
  - Clean cornering at high speed
  - Near-miss traffic
  - Maintaining “flow” multiplier (continuous good driving)
- Penalties:
  - Barrier hits / ditch hits
  - Stalling / reversing / extreme slowdowns
  - Repeated off-road grinding

**MVP:** keep scoring readable: 2–3 primary events + multiplier.

---

## 4. World / Setting: Nordic Forest Only

No biome switching. Variety comes from parameters:

- **Season:** summer / autumn / winter / spring
- **Time:** day / dusk / night
- **Weather:** clear / rain / snow / fog
- These affect:
  - Visibility (fog/night)
  - Surface conditions (wetness, snow/ice patches)
  - Audio/FX palette (wind, snow hiss, rain)

---

## 5. Vehicle Physics (RWD-First, AWD-Capable)

### 5.1 Architecture
- **Single Rigidbody** (box/capsule collider is fine for MVP).
- **4 raycast wheels** (ray or spherecast).
- Suspension via **spring + damper** force at hit point.
- Tire forces computed from **slip velocity**, not WheelCollider.

### 5.2 Drivetrain Requirements
- Implementation supports AWD torque split, but default config is **rear-biased** (e.g., 0.85–1.00 rear).
- RWD should be the first “fun/feel” target.
- Rear axle has a **drift-friendly LSD** (power lock > coast lock).

### 5.3 Stability Aids (all configurable)
- **Yaw damping** (small, only when grounded).
- **Anti-roll** (optional).
- **Auto-straighten assist** (low default; disableable).
- **Roll stability blend** (optional): blend local-down vs world-down suspension ray direction.

---

## 6. Surfaces (Single Pipeline)

### 6.1 Data Encoding (Vertex Colors)
Road mesh vertex color channels encode surface mix:

- **R:** asphalt weight
- **G:** gravel/shoulder weight
- **B:** ice/snow weight
- **A:** wetness

Wheel raycast hit reads triangle vertex colors via **barycentric interpolation**.

### 6.2 Gameplay Use
- Surface mix feeds:
  - **Friction (μ)**
  - Tire audio + particles
  - Camera shake / haptics hooks
- Friction model:
  - Compute base μ from (asphalt/gravel/ice).
  - Apply wetness reduction.
  - Apply **combined friction ellipse**: braking + cornering share grip budget.

---

## 7. Road Generation (Runtime)

### 7.1 Output
For each generated chunk:
- Centerline samples (distance-along-road **s**)
- Extruded mesh using a **RoadProfile** (cross-section samples)
- MeshCollider (optionally simplified later)
- Vertex colors painted from season/weather rules

### 7.2 Determinism
- All generation driven by **seed**.
- Road is built as a stream of parametric segments, sampled into a polyline.

### 7.3 Chunking + Seams
- Fixed chunk length in meters (e.g., 120–160 m).
- **Seam-safe sampling:** chunk N includes end sample; chunk N+1 excludes its start sample so vertices match exactly.
- Culling/generation based on **player distance s**, not world Z.
- Pool chunks to avoid allocations.

### 7.4 Frames + Banking
- Use **parallel transport frames** for stable right/up vectors over hills.
- Banking derived from curvature proxy (tangent delta / ds), clamped.

---

## 8. Traffic (MVP)

- Two-lane traffic as moving obstacles.
- Simple behavior:
  - Drive forward at speed.
  - Optional gentle lateral “avoid” nudge.
- Spawn density scales with progress and/or difficulty.

---

## 9. Service Stops (Post-MVP)

**Not in MVP.** When introduced:

- Stops appear at **distance/section intervals** (not biome transitions).
- While stopped, time buffer drains at baseline threat speed.

Initial stop actions:
- **Random upgrade parts (roguelike):** choose 1 of 2–3 offers; rarity + ±stat wiggle.
- **Repairs:** reduce damage/penalties.
- **Intel:** preview upcoming section traits (curves/surfaces/hazards).

Costs:
- **Time buffer** + **style points**.

No tuning in the first stop implementation.

---

## 10. Implementation Plan (Engineering)

### Phase A — Road + Surfaces
1. RoadStream seeded segment generator (segment params → centerline samples).
2. RoadProfile extrusion → Mesh + MeshCollider.
3. Seam-safe chunking + pooling; generate/cull by player **s**.
4. Surface painting (season/weather) → vertex colors.
5. SurfaceResolver: raycast hit → barycentric color sample → μ.

### Phase B — Car Physics (RWD-first)
1. Rigidbody + wheel raycast suspension.
2. Slip-based tire forces + μ scaling.
3. Combined friction ellipse clamp.
4. Rear LSD (power lock) + AWD torque split support (rear-biased default).
5. Stability aids as toggles/curves (yaw damping, anti-roll, assist).

### Phase C — Gameplay Loop
1. Time buffer + threat pacing.
2. Scoring + multiplier.
3. Traffic spawner + near-miss scoring.
4. Meta progression between runs.

### Phase D — Post-MVP
- Service stops (roguelike parts)
- More surface rules (ice patches, wetness bands)
- More car archetypes / drivetrains after RWD is proven

---

## 11. Configuration Surfaces (ScriptableObjects)

Recommended assets:
- `CarHandlingConfig` (mass/COM, aero, stability aids)
- `TireConfig` (front/rear stiffness, saturation speeds, sliding drag)
- `DrivetrainConfig` (rear bias, LSD params, AWD behavior)
- `SurfaceFrictionConfig` (μ values, wetness reduction, speed curve)
- `RoadProfile` (cross-section samples + band tags)
- `RoadGenerationConfig` (chunk length, sampling density, curvature/hills)
- `DifficultyConfig` (threat speed curve, traffic density curve, score multipliers)

---

## 12. Notes / Non-Goals (MVP)

- No multiple biomes.
- No pit/store UI.
- No deep car tuning UI.
- No complex racing AI.

