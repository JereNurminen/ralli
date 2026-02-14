# Ralli — Claude Code Project

## Project

Endless rally driving game set in Nordic forests. RWD-focused vehicle physics, procedural road generation, style-based scoring, and roguelike chase pressure.

- **Engine:** Unity 6 LTS (6000.0.45f1)
- **Rendering:** Universal Render Pipeline (URP)
- **Input:** New Input System
- **Splines:** Unity Splines 2.8.3 (road piece authoring)
- **Platform:** Desktop (MVP)

## Architecture Decisions

- **Vehicle physics first.** The car must feel great on a flat plane before roads exist.
- **Raycast wheels**, not WheelCollider. Suspension via spring+damper, tire forces from slip velocity.
- **RWD-first** with AWD support (rear-biased torque split).
- **Vertex color encoding** for surface data (R=asphalt, G=gravel, B=ice/snow, A=wetness).
- **Seeded procedural generation** for deterministic road streams.
- **Designed road pieces** — spline-authored road sequences (hairpins, chicanes, S-curves) baked to AnimationCurve data, placed by the RoadStream among procedural filler with heading-correction weighting.
- **ScriptableObjects** for all configuration (car handling, tires, drivetrain, surfaces, road gen, difficulty).

## Implementation Phases

1. **Phase A — Vehicle Physics** (flat plane, RWD-first)
2. **Phase B — Surfaces + Road Generation**
3. **Phase C — Gameplay Loop** (chase, scoring, traffic, meta)
4. **Phase D — Post-MVP** (service stops, more surfaces, car archetypes)

## C# Conventions

- **Standard Unity/.NET style**
- PascalCase for methods, properties, types
- camelCase for private fields
- `_` prefix for backing fields
- **No namespaces** (flat Unity style)
- No `#region` blocks
- `[SerializeField]` for inspector-exposed private fields
- `[Header("Section")]` to group inspector fields

## Folder Structure

```
Assets/
  Scripts/
    Vehicle/       # Car physics, suspension, drivetrain, input
    Road/          # Road generation, chunking, profiles, designed pieces
      Editor/      # Spline authoring tools (editor-only)
    Surfaces/      # Surface resolver, friction model
    Gameplay/      # Scoring, chase/threat, traffic, meta
    Core/          # Shared utilities, camera, game state
  Materials/
  Prefabs/
  ScriptableObjects/
  Scenes/
  Settings/        # URP and render pipeline assets
```

## Development Workflow

- **Unity MCP** is available for direct editor interaction (scene setup, script creation, component management, testing).
- After creating or modifying scripts, always check the Unity console for compilation errors before proceeding.
- Use `read_console` after script changes to verify compilation.
- Run tests with `run_tests` to validate changes.
- Design docs live in `docs/`, implementation plans in `docs/plans/`.

## Key Documentation

- `docs/` — Game design specifications (00–12)
- `docs/plans/` — Implementation plans and design documents
- `docs/README.md` — Index of all documentation
