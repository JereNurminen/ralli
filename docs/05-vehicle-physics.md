# 5. Vehicle Physics (RWD-First, AWD-Capable)

## 5.1 Architecture
- **Single Rigidbody** (box/capsule collider is fine for MVP).
- **4 raycast wheels** (ray or spherecast).
- Suspension via **spring + damper** force at hit point.
- Tire forces computed from **slip velocity**, not WheelCollider.

## 5.2 Tire Model
- **AnimationCurve-based slip curves** for both lateral and longitudinal grip.
- Lateral: slip speed (scaled by `lateralSlipScale`) → grip factor (0..1). Curve has a peak then falloff, giving natural breakaway behavior.
- Force is **load-based**: `gripFactor × lateralGrip × wheelLoad`, not velocity-proportional.
- **Friction circle** clamp on combined lateral+longitudinal force per wheel (`wheelLoad × tireFriction`).
- When longitudinal force (drive/boost) saturates the circle, lateral grip budget is reduced → oversteer.

## 5.3 Boost
- Dedicated button (keyboard: Left Shift, gamepad: Button West / X).
- Multiplies rear wheel drive force by `boostForceMultiplier` (default 4.0×).
- Smooth ramp on/off via `boostRampUpSpeed` / `boostRampDownSpeed`.
- **When straight**: excess drive force causes wheel spin (burnout), traction returns as car accelerates.
- **When turning**: high longitudinal force saturates friction circle → lateral grip drops → rear kicks out (throttle kick oversteer).
- Player controls drift by modulating boost and steering angle.
- Future: overheat mechanic where boost increases heat.

## 5.4 Drift Initiation
- **Boost + turn**: primary method. Boost overwhelms friction circle, rear breaks away.
- **Handbrake**: reduces rear lateral grip to `rearGripWhileHandbrake` (default 0.35×), good for initiating slides at lower speeds.
- **Scandinavian flick**: turn-in → lift-off → weight transfers forward → rear unloads past slip curve peak → breakaway. Enabled by `straighteningActivationDelay` (0.12s) which prevents the straightening assist from killing rotation during quick steer transitions.

## 5.5 Drivetrain
- Implementation supports AWD torque split, but default config is **rear-biased** (e.g., 0.85–1.00 rear).
- RWD should be the first "fun/feel" target.
- Rear axle has a **drift-friendly LSD** (power lock > coast lock).
- **Reverse**: activated by holding brake when nearly stopped. Swaps throttle/brake roles; drive force applied in reverse direction. Exits when throttle pressed while nearly stopped.

## 5.6 Stability Aids (all configurable)
- **Yaw damping** (small, only when grounded).
- **Anti-roll** (optional).
- **Auto-straighten assist** (low default; disableable). Gated behind `straighteningActivationDelay` to allow flick-style drift initiation.
- **Roll stability blend** (optional): blend local-down vs world-down suspension ray direction.
