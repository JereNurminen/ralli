# Entry Spec

- `entry_id`: `2026-02-18-boost-drift-reverse`
- `timestamp_utc`: `2026-02-18T12:00:00Z`
- `commit`:
  - `hash`: pending
  - `short`: pending
  - `title`: `Add boost, slip-curve tire model, drift mechanics, and reverse`
- `phase`: `PhaseA`
- `scope`:
  - `vehicle.physics.tire_model`
  - `vehicle.physics.boost`
  - `vehicle.physics.reverse`
  - `vehicle.input.input_system`
  - `vehicle.stability_assists`
  - `debug.overlay`

## Changes

### Tire model: linear → slip-curve (load-based)
- `Ralli/Assets/Scripts/Vehicle/CarController.cs`: replaced linear lateral force (`-lateralSpeed × lateralGrip × mass`) with AnimationCurve-based slip curve lookup. Force is now load-based (`gripFactor × lateralGrip × wheelLoad`).
- `Ralli/Assets/Scripts/Vehicle/CarHandlingConfig.cs`: added `lateralSlipCurve` (AnimationCurve), `lateralSlipScale`, `longitudinalSlipCurve` (AnimationCurve, reserved for future use), `longitudinalSlipScale`.
- Friction circle clamp unchanged; lateral slip curve provides peak-then-falloff grip characteristic for natural breakaway.

### Boost
- `Ralli/Assets/Scripts/Vehicle/CarController.cs`: added `boostFactor` (0..1) smoothly ramped via `MoveTowards` each FixedUpdate. Rear wheel drive force multiplied by `1 + boostFactor × (boostForceMultiplier - 1)`. Public `IsBoosting` and `BoostFactor` properties.
- `Ralli/Assets/Scripts/Vehicle/CarHandlingConfig.cs`: added `boostForceMultiplier` (default 4.0), `boostRampUpSpeed` (default 8), `boostRampDownSpeed` (default 6).
- Mechanism: boost saturates friction circle → lateral grip budget reduced → rear kicks out when turning; wheel spin when straight.

### Reverse
- `Ralli/Assets/Scripts/Vehicle/CarController.cs`: added `inReverse` state. Enters reverse when brake held while forward speed < 0.5 m/s. Exits when throttle pressed while forward speed > -0.5 m/s. When in reverse, throttle/brake inputs are swapped and drive force direction is negated. Public `InReverse` property.

### Straightening assist fix (Scandinavian flick support)
- `Ralli/Assets/Scripts/Vehicle/CarController.cs`: `ApplyStraighteningAssist()` now gated behind `nearZeroSteerTime` timer. Assist only fires after steer has been near-zero for `straighteningActivationDelay` seconds (default 0.12s).
- `Ralli/Assets/Scripts/Vehicle/CarHandlingConfig.cs`: added `straighteningActivationDelay` (default 0.12).
- Prevents assist from killing yaw rotation during quick steer transitions (flick).

### Input system overhaul
- `Ralli/Assets/InputSystem_Actions.inputactions`: replaced default Unity template with rally-specific "Driving" action map (Steer, Throttle, Brake, Handbrake, Boost). Enabled C# class generation.
- `Ralli/Assets/InputSystem_Actions.inputactions.meta`: set `generateWrapperCode: 1`, `wrapperClassName: InputSystem_Actions`.
- `Ralli/Assets/Scripts/Vehicle/CarInputReader.cs`: rewrote from ~100 lines of raw `Keyboard.current`/`Gamepad.current` polling to ~30 lines using generated `InputSystem_Actions` class. Added `Boost` property.

### Debug overlay
- `Ralli/Assets/Scripts/Vehicle/Debug/VehicleDebugInfoProvider.cs`: added `Input Boost`, `Boosting`, `Boost Factor` fields.

## Parameters
- `handling.lateralSlipScale`: `4.0`
- `handling.lateralSlipCurve`: `0→0, 0.5→0.85, 1.0→1.0, 2.0→0.78, 4.0→0.55`
- `handling.longitudinalSlipCurve`: `0→0, 0.15→1.0, 0.35→0.82, 1.0→0.55, 2.0→0.40`
- `handling.longitudinalSlipScale`: `1.0`
- `handling.boostForceMultiplier`: `4.0`
- `handling.boostRampUpSpeed`: `8.0`
- `handling.boostRampDownSpeed`: `6.0`
- `handling.straighteningActivationDelay`: `0.12`
- `input.boost.keyboard`: `Left Shift`
- `input.boost.gamepad`: `Button West (X)`
- `reverse.enterThreshold_mps`: `0.5`
- `reverse.exitThreshold_mps`: `-0.5`

## Input Bindings
| Action | Keyboard | Gamepad |
|---|---|---|
| Steer | A/D, Left/Right arrows | Left Stick X |
| Throttle | W, Up arrow | Right Trigger |
| Brake | S, Down arrow | Left Trigger |
| Handbrake | Space | Button South (A) |
| Boost | Left Shift | Button West (X) |

## Scene Changes
- None (no scene modifications required).

## Validation
- `unity.refresh_compile`: `pass`
- `unity.console_errors`: `none`
- `unity.console_warnings`: `none`

## Known Issues
- `boost_tuning`: default `boostForceMultiplier` (4.0) may need further increase for stronger drift initiation; tuning in progress.
- `lateral_grip_tuning`: `frontLateralGrip` (10.5) and `rearLateralGrip` (7.0) carry over from the old velocity-based model and may need adjustment for the new load-based model.
- `longitudinal_slip_curve`: currently unused (removed from force pipeline to avoid double-penalizing with friction circle). Curve config fields retained for future use.

## Next
- Tune boost and grip parameters for target drift feel.
- Add overheat mechanic (boost increases heat, heat limits boost availability).
- Add drift detection (slip angle + duration tracking) for scoring system.
- Visual/audio feedback for boost and drift (particles, tire smoke, engine sound).

## Links
- `docs/05-vehicle-physics.md`
- `docs/11-configuration.md`
