# Phase 3 — Driving dynamics

Depends on: Phase 2.

## Goal and feel targets

- Back-road hot hatch. Fun at ~100 km/h, dangerous at ~120 km/h.
- Understeer on Normal throttle. Oversteer/drift via Overdrive. Handbrake is a secondary, non-primary drift aid.
- Realism is a tool: hand-authored, playtested curves; nothing derived from real tire/suspension physics.
- Fully playable on digital inputs.

## Current state (verified)

- `CarController` simulates 4 raycast wheels with spring/damper suspension, anti-roll, slip-curve lateral/longitudinal forces, a friction circle (with a boost-blended reprioritization), and stacked grip/drive multipliers (single-rear-wheel-grounded, handbrake, front-airborne), plus straightening assist and angular damping.
- `CarDriveModel`: automatic fake gears (`GearIndex`, `GearRpm01` from normalized speed and `fakeGearThresholds01`, with shift-pause torque dips) and `StepBoostFactor` (MoveTowards ramp using `boostRampUpSpeed` / `boostRampDownSpeed`).
- `CarController` exposes `SpeedMps`, `BoostFactor`, `FakeRpm01`.
- `CarHandlingConfig` has ~50 fields.

## Model

Two axle grip values (front, rear), driven directly by input. No shared weight-transfer scalar.

| Input state | Effect |
|-------------|--------|
| Normal throttle + steering | Front grip target drops (nose pushes wide) |
| Overdrive + steering | Rear grip target drops hard (tail steps out); recovers when Overdrive or steering eases |
| Overdrive + wheel straight | Flat power multiplier (straight-line speed boost) |
| Handbrake | Rear grip floor, applied instantly (no ramp); most useful at low speed/hairpins where Overdrive has little force to dump |
| Brake | Decelerates only. No rotation effect. |

- Each grip target is smoothed with its own time constant, ramping over **time held**, not input magnitude.
- Lateral demand scales with speed² for a given turn radius (geometric, not authored). Author grip curves so demand is comfortably below grip at ~100 km/h and near/over it at ~120 km/h on typical corners.
- Grip reduction as demand exceeds availability: hand-authored `AnimationCurve`, not a tire formula.

## Tasks

### 3.1 Core model

- [ ] Replace per-wheel raycasts with a single center raycast for grounded state and ground normal.
- [ ] Consolidate `CarSteeringModel`, `CarDriveModel`, `WheelForceModel`, and the wheel-simulation half of `CarController` into one motor component (suggested: `CarMotor.cs`).
- [ ] Steering: simplified wheel angle feeding lateral demand; heading is an outcome, not a direct set.
- [ ] Apply lateral grip correction **per axle** (at front and rear axle positions, scaled by that axle's grip) so grip asymmetry produces yaw. **Verify by prototype:** a single whole-car velocity blend cannot distinguish understeer from oversteer; if the per-axle version is unstable, fall back to a whole-car blend plus an explicit yaw term.
- [ ] Implement the input→grip-target table above with per-target smoothing.
- [ ] Overdrive power multiplier (config; upgrade axis, see 3.3).
- [ ] Remove: friction circle, slip curves, boost friction reprioritization, straightening assist, angular damping, anti-roll, per-wheel suspension, and the edge-case multipliers listed under Current state.
- [ ] Trim `CarHandlingConfig` to: front/rear grip curves and response time constants, Overdrive power multiplier, base acceleration, max speed, drag, steering (turn rate, falloff at speed), handbrake grip floor, heat params (3.2).

### 3.2 Engine heat (replaces any boost/nitro gauge)

- [ ] `Heat01` scalar: rises while Overdrive is held, decays otherwise. Reuse the `StepBoostFactor` MoveTowards pattern with separate rise/fall rates.
- [ ] Near max heat, power tapers (default). Optional harder failure (temporary power loss/stall) past a second threshold. Curve and threshold are playtest-tuned.
- [ ] No visible meter beyond the cockpit heat gauge (Phase 4) and audio cues (Phase 6).

### 3.3 Upgrades (checkpoints)

- [ ] Discrete hand-tuned tiers, not a continuous slider (e.g. tier 2 targets "fun at 130, dangerous at 150"). Each tier gets its own playtest pass.
- [ ] At least two independent axes: **power** (acceleration, top speed, Overdrive multiplier) and **grip** (grip ceilings, response times). Raising grip ceiling raises the speed at which a corner becomes dangerous, automatically, via the speed² relationship.
- [ ] Late tiers may sharpen response times (faster and less forgiving), not only raise ceilings.

### 3.4 Outputs consumed by later phases

- [ ] Expose: `SpeedMps` (exists), `Heat01`, per-axle grip usage `0..1` (for tire audio/FX), impact events with magnitude, lateral G and longitudinal G (camera sway), grounded state, and the active surface type under the car.
- [ ] **[STRETCH]** Keep `GearIndex` / `GearRpm01` / `FakeRpm01` from `CarDriveModel` as cosmetic outputs only (dashboard tachometer, hand-shift animation). Strip their influence on drive force and remove shift-pause torque dips.

## Out of scope (removed features; do not re-add)

Manual transmission, clutch, downshift input, automatic-shift setting, lift-off oversteer, trail-braking rotation, shared `weightBias` scalar, boost/nitro gauge.
