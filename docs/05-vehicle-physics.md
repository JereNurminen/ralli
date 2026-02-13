# 5. Vehicle Physics (RWD-First, AWD-Capable)

## 5.1 Architecture
- **Single Rigidbody** (box/capsule collider is fine for MVP).
- **4 raycast wheels** (ray or spherecast).
- Suspension via **spring + damper** force at hit point.
- Tire forces computed from **slip velocity**, not WheelCollider.

## 5.2 Drivetrain Requirements
- Implementation supports AWD torque split, but default config is **rear-biased** (e.g., 0.85–1.00 rear).
- RWD should be the first "fun/feel" target.
- Rear axle has a **drift-friendly LSD** (power lock > coast lock).

## 5.3 Stability Aids (all configurable)
- **Yaw damping** (small, only when grounded).
- **Anti-roll** (optional).
- **Auto-straighten assist** (low default; disableable).
- **Roll stability blend** (optional): blend local-down vs world-down suspension ray direction.
