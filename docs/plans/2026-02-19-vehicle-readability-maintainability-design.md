# Vehicle Readability and Maintainability Design

## Context
Current vehicle behavior is concentrated in `Ralli/Assets/Scripts/Vehicle/CarController.cs`, which combines orchestration, steering logic, drive state transitions, force math, and wheel simulation details. This works, but it makes local reasoning and safe edits harder over time.

## Goals
- Improve readability and maintainability of vehicle runtime code.
- Preserve existing inspector workflow and ScriptableObject-driven tuning (`CarHandlingConfig`).
- Keep external behavior and public integration points stable for visuals/debug/effects.
- Add lightweight tests where they are cheap and high-value.

## Non-Goals
- Full architecture rewrite into many scene components.
- Replacing ScriptableObject config with code-only configuration.
- Introducing heavy test infrastructure if it blocks practical refactoring.

## Chosen Approach
Use a **light decomposition** of `CarController`:
- Keep `CarController` as the single MonoBehaviour orchestrator and inspector-facing integration point.
- Extract focused helper/model classes in `Ralli/Assets/Scripts/Vehicle/` for pure or near-pure logic.
- Keep serialized fields and existing config shape intact.

## Architecture
- `CarController` remains responsible for Unity lifecycle, rigidbody integration, wheel loop orchestration, and telemetry exposure.
- New helper classes encapsulate logic clusters:
  - `CarSteeringModel`: steer fade, steer response stepping, airborne steering authority.
  - `CarDriveModel`: reverse state transitions, fake gear/rpm logic, base force modulation, boost ramping.
  - `WheelForceModel`: isolated wheel force computations/slip math helpers used by wheel simulation.
- Wheel runtime data remains private to `CarController`, but can be grouped into clearer internal structures.

## Data Flow
`FixedUpdate` becomes a readable, explicit pipeline:
1. Validate runtime prerequisites (config/components).
2. Compute steering and drive runtime state.
3. Simulate wheels using shared helper calculations.
4. Apply anti-roll and passive stabilization.
5. Publish/update telemetry consumed by visuals and debug tooling.

## Error Handling / Guardrails
- Centralize null/config guards at frame entry.
- Replace repeated wheel reset branches with a single helper.
- Keep all public methods and properties currently consumed by:
  - `Ralli/Assets/Scripts/Vehicle/CarWheelVisuals.cs`
  - `Ralli/Assets/Scripts/Vehicle/TireMarkRenderer.cs`
  - `Ralli/Assets/Scripts/Vehicle/TireSmokeEmitter.cs`
  - `Ralli/Assets/Scripts/Vehicle/Debug/VehicleDebugInfoProvider.cs`

## Testing Strategy
No tests currently exist under `Ralli/Assets/Tests`. Add minimal EditMode tests for pure helper logic only:
- `CarSteeringModelTests` for steer fade and airborne steering authority.
- `CarDriveModelTests` for reverse toggling, gear banding, and boost ramp behavior.

If Unity assembly/test setup overhead becomes disproportionate, prioritize refactor correctness and runtime validation, then add tests incrementally.

## Risks and Mitigations
- Risk: subtle behavior drift due to extracted logic.
  - Mitigation: preserve formulas/thresholds exactly where possible, add model tests, and run Unity tests.
- Risk: hidden coupling from current call order.
  - Mitigation: preserve update order in `FixedUpdate` and avoid public API changes.

## Success Criteria
- `CarController` is materially shorter and easier to scan.
- Extracted helpers have clear single responsibilities and names.
- Existing vehicle-dependent scripts compile and run without API changes.
- Relevant tests run successfully (or limitations clearly reported).
