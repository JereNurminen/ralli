# Ralli — Implementation Plan

Technical spec for agentic implementation of the Ralli Unity project (URP).
Phases are separate files. **Strictly linear: finish a phase before starting the next. No parallel work.**

## Phase order

| # | File | Scope | Depends on |
|---|------|-------|------------|
| 1 | `01-terrain-generation.md` | Road/terrain structure and generation | — |
| 2 | `02-controls.md` | Input mapping | — |
| 3 | `03-driving-dynamics.md` | Car model, Overdrive, engine heat, upgrades | 2 |
| 4 | `04-in-car-graphics.md` | Cockpit, camera, hands, screen FX, mirrors | 3 |
| 5 | `05-world-graphics.md` | Forest/terrain visuals, biomes | 1 |
| 6 | `06-audio.md`, `06b-audio-asset-sources.md` | Car/tire/impact audio, asset sources | 3 (interior mix: 4) |

Phase 6 was added after the ordering was set; move it earlier if wanted (it only needs Phase 3 outputs).

## Conventions

- `[ ]` = task. **[STRETCH]** = non-MVP; skip for the first playable.
- **Verify:** = assumption not confirmed in code; check before relying on it.
- Line numbers are approximate; search by symbol.

## Non-negotiables

- Procedural road generation
- Pre-authored (designed) road segments
- Nordic forest biome

## Global design constraints

- Lofi visuals are a tool: fog, decals, and texture tricks replace real geometry wherever gameplay does not need it.
- Realism is a tool, never a goal. Hand-authored, playtested curves over derived physics.
- Must work fully on digital (non-analog) throttle/brake input.
- In-car camera only. Target tone: anxious, stressed (Colin McRae Rally 2.0 era).
- Deterministic generation: the road generator derives randomness from `Hash01(index, salt)`; keep new generation seed-deterministic.

## Codebase map

Root: `Ralli/Assets/Scripts/`

| Path | Role |
|------|------|
| `Road/RoadStreamGenerator.cs` (~1970 lines) | Chunk-streamed road + terrain mesh, elevation, curves, tree scatter |
| `Road/RoadGenerationConfig.cs` | All road/terrain tunables |
| `Road/DesignedRoadPieceAuthoring.cs`, `DesignedRoadPiece.cs`, `DesignedRoadPiecePool.cs`, `Editor/DesignedRoadPieceAuthoringEditor.cs` | Hand-authored segments baked from Unity splines |
| `Road/RailPlacementUtility.cs`, `RailingMeshBuilder.cs` | Guard rails |
| `Vehicle/CarController.cs` (~630 lines) | Per-wheel raycast suspension + tire forces (to be replaced) |
| `Vehicle/CarDriveModel.cs` | Drive force, fake gears, `StepBoostFactor` |
| `Vehicle/CarSteeringModel.cs`, `WheelForceModel.cs` | Steering factor; slip-curve tire forces |
| `Vehicle/CarHandlingConfig.cs` | ~50 tunables |
| `Vehicle/CarInputReader.cs` | `Steer`, `Throttle`, `Brake` (float); `Handbrake`, `Boost` (bool) |
| `Vehicle/CarWheelVisuals.cs`, `TireMarkRenderer.cs`, `TireSmokeEmitter.cs` | Wheel/tire visuals |
| `Core/FollowCamera.cs` | Third-person chase cam (to be replaced) |
| `Core/BoostPostProcessing.cs` | URP motion blur + chromatic aberration keyed to `BoostFactor` |
| `Traffic/*` | Traffic streaming |

No audio, cockpit, IK, or animation code exists yet.
