# In-Car Camera + Driving Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put the camera at the driver's eye point (rigid, no chase cam) and replace the per-wheel tire simulation with a small, arcade two-axle grip model driven by input.

**Architecture:** `InCarCamera` copies the car's interpolated pose plus a fixed eye offset every `LateUpdate`. `CarController` is rewritten in place as one self-contained motor component (~250 lines). It uses one center raycast for ground support and alignment, one drive force at the center of mass, and one clamped sideways grip force per axle. Grip asymmetry between the axles is what makes the car understeer or oversteer. Tire FX and wheel visuals are deleted because they're invisible from the in-car view.

**Tech Stack:** Unity 6 LTS (6000.0.45f1), URP, New Input System, Unity MCP for editor actions.

**Spec:** `docs/2026-09-plan/03-driving-dynamics.md` (3.1, 3.2), `docs/2026-09-plan/02-controls.md`, `docs/2026-09-plan/04-in-car-graphics.md` (4.2 camera placement only). The user reordered the phases: in-car camera + driving first, road generation afterwards.

## Global Constraints

- **Simplicity over simulation.** The target is a tense but arcade-y feel. Prefer a few readable forces over clever math. No unit tests: verify by compiling and playtesting.
- Must work fully on digital (non-analog) throttle/brake input.
- In-car camera only.
- Out of scope, do not add: manual transmission, clutch, downshift input, automatic-shift setting, lift-off oversteer, trail-braking rotation, shared `weightBias` scalar, boost/nitro gauge.
- Brake decelerates only. No rotation effect.
- C#: no namespaces, no `#region`, PascalCase methods/properties/types, camelCase private fields, `[SerializeField]` + `[Header]`, Allman braces, braces on every `if`.
- All tuning lives in `CarHandlingConfig` (ScriptableObject).
- After every script change: Unity console must show 0 compile errors (Unity MCP `read_console`, or the Console window).

## Decisions taken in this plan

1. **`CarController` is rewritten in place** (not renamed `CarMotor`). This keeps the script GUID, so the scene, `TrafficVehicle`, `BoostPostProcessing` and `VehicleDebugInfoProvider` keep working.
2. **Everything lives in one file.** `CarDriveModel`, `CarSteeringModel` and `WheelForceModel` are deleted. The few lines of steering, reverse and Overdrive logic they held move into `CarController`. Their EditMode tests go with them.
3. **Removed as invisible from the in-car view:** `TireMarkRenderer`, `TireSmokeEmitter`, `CarWheelVisuals`, the `TireSmoke` prefab, and the `TireMarks`/`TireSmoke` materials. **Kept:** `BoostPostProcessing`, a screen effect that is visible in-car.
4. **Grip model:** per axle, sideways force = `-slipSpeed × gripStiffness × axleMass`, clamped at `gripG × g × axleMass`. There is no slide curve: past the clamp, the axle just slides. Each axle's grip multiplier ramps linearly toward its target over its response time, so it follows how long the input is held.
5. **Top speed comes from drag:** slowdown = `rollingDeceleration + baseAcceleration × (speed / maxSpeed)²`. Overdrive raises the top speed to ≈ `maxSpeed × sqrt(overdrivePowerMultiplier)`.
6. **Deferred:** upgrades (3.3), heat stall, G-force and impact outputs (add them with the Phase 4 camera sway that needs them), surface type output (road rework), cockpit.

## Default values: numeric sanity check

A Python port of the grip force model (dt 0.02 s, mass 1250 kg, axles ±1.3 m, `gripStiffness` 12, front 1.15 g, rear 1.3 g) gave:

| Case | Result |
|------|--------|
| Radius-80 m corner at 100 km/h | holds the line (front grip 85% used) |
| Same corner at 120 km/h | front 122% used: understeers wide |
| Digital full lock at 100 km/h, normal throttle | drift < 1°: understeer, no spin |
| 100 km/h, rear grip 0.45 (Overdrive + steer) | 11° slide at 0.5 s, 42° at 1 s |
| Sideways slip 3 m/s + yaw 1 rad/s, wheel straight | settles to 0 in < 2 s, no oscillation |

## Play-check focus (things most likely to break)

1. Digital full lock at ~100 km/h on normal throttle: stable push wide, never a spin.
2. Standing still with full steer, or brake held at 0 km/h: no yaw, no creep, no jitter.
3. Reverse with steer: turns the expected way, no spin.
4. Handbrake in a slow hairpin: tail steps out immediately, with no ramp-in.
5. Airborne over a crest: no drive or grip in the air, and it lands without bouncing or flipping.

Not covered: no recovery if the car ends up on its side or roof. The old controller had none either.

---

## File map

| File | Action |
|------|--------|
| `Ralli/Assets/Scripts/Core/InCarCamera.cs` | Create |
| `Ralli/Assets/Scripts/Core/FollowCamera.cs` | Delete |
| `Ralli/Assets/Scripts/Vehicle/CarController.cs` | Rewrite |
| `Ralli/Assets/Scripts/Vehicle/CarHandlingConfig.cs` | Rewrite |
| `Ralli/Assets/Scripts/Vehicle/CarInputReader.cs` | `Boost` → `Overdrive` |
| `Ralli/Assets/Scripts/Vehicle/Debug/VehicleDebugInfoProvider.cs` | Rewrite body |
| `Ralli/Assets/Scripts/Core/BoostPostProcessing.cs` | Read `OverdriveFactor` |
| `Ralli/Assets/Scripts/Vehicle/CarDriveModel.cs`, `CarSteeringModel.cs`, `WheelForceModel.cs` | Delete |
| `Ralli/Assets/Scripts/Vehicle/TireMarkRenderer.cs`, `TireSmokeEmitter.cs`, `CarWheelVisuals.cs` | Delete |
| `Ralli/Assets/Prefabs/TireSmoke.prefab`, `Materials/TireMarks.mat`, `Materials/TireSmoke.mat` | Delete |
| `Ralli/Assets/Tests/EditMode/Editor/Vehicle/` (whole folder) | Delete |
| `Ralli/Assets/Scenes/SampleScene.unity` | Component swaps |

Delete each file's `.meta` along with it. In the scene, always remove a component **before** deleting its script, so no missing-script references are left.

**Branch:** `feature/in-car-driving` from `master`.

---

### Task 1: In-car camera (≈20 min)

**Files:** Create `Ralli/Assets/Scripts/Core/InCarCamera.cs`; delete `Ralli/Assets/Scripts/Core/FollowCamera.cs`; scene `Main Camera`.

Context: the car root is a Cube scaled `(1.82, 1.46, 4.2)`, so use `position + rotation * offset` (meters), not `TransformPoint`. Rigidbody interpolation is already on, so `LateUpdate` is smooth. The camera sits inside the cube, whose faces are back-face culled, so the body is invisible. That matches "no car model yet".

- [ ] **Step 1: Create `InCarCamera.cs`**

```csharp
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class InCarCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Eye Position")]
    [Tooltip("Driver eye offset from the car pivot in meters, car-local axes (ignores car scale). Left-hand drive = negative X.")]
    [SerializeField] private Vector3 eyeOffset = new Vector3(-0.37f, 0.2f, -0.15f);
    [Tooltip("Downward pitch in degrees.")]
    [SerializeField] private float pitchDegrees = 4f;

    [Header("Lens")]
    [Tooltip("Vertical field of view. Narrow and constant; no speed ramp.")]
    [SerializeField] private float fieldOfView = 55f;
    [SerializeField] private float nearClipPlane = 0.05f;

    private Camera cachedCamera;

    private void Start()
    {
        cachedCamera = GetComponent<Camera>();

        if (target == null)
        {
            CarController carController = FindFirstObjectByType<CarController>();
            if (carController != null)
            {
                target = carController.transform;
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.SetPositionAndRotation(
            target.position + target.rotation * eyeOffset,
            target.rotation * Quaternion.Euler(pitchDegrees, 0f, 0f)
        );
        cachedCamera.fieldOfView = fieldOfView;
        cachedCamera.nearClipPlane = nearClipPlane;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
```

- [ ] **Step 2: Swap in the scene.** On `Main Camera`: add `InCarCamera`, remove `FollowCamera`, and save the scene. Then delete `FollowCamera.cs` + `.meta`.
- [ ] **Step 3: Verify.** The console shows 0 errors, and `grep -rn FollowCamera Ralli/Assets` returns nothing.
- [ ] **Step 4: Play check.** Drive for 30 s. The view is from the driver's seat, left of center, with the road below the horizon, no body visible, and no jitter at speed. Tune `eyeOffset` on the component if needed.
- [ ] **Step 5: Commit** — `git commit -m "Replace chase cam with rigid in-car camera"` (stage the new script + meta, the deleted FollowCamera files, and the scene).

---

### Task 2: Remove tire FX and wheel visuals (≈15 min)

**Files:** delete `TireMarkRenderer.cs`, `TireSmokeEmitter.cs`, `CarWheelVisuals.cs`, `Prefabs/TireSmoke.prefab`, `Materials/TireMarks.mat`, `Materials/TireSmoke.mat` (+ metas); scene `Car`.

- [ ] **Step 1: Remove from the scene.** On `Car`: remove the `TireMarkRenderer`, `TireSmokeEmitter` and `CarWheelVisuals` components. Delete any leftover `TireSmoke` instances and any wheel mesh or tire-mark GameObjects those components created or left in the scene. Save the scene.
- [ ] **Step 2: Delete the files** listed above.
- [ ] **Step 3: Verify.** The console shows 0 errors. `grep -rlE "c6e230a1be7854e028aafa2fb7382a49|c44fa7fa560d84ef6bcb950d2e69b026|d40e60ee26ecf48fb8cdf5d91e3dd0c7|ace0efcf2aeba4260b1b6e98a2bd700c" Ralli/Assets` returns nothing. These are the GUIDs of the three scripts and the prefab.
- [ ] **Step 4: Play check.** The car still drives exactly as before, and nothing is logged as missing.
- [ ] **Step 5: Commit** — `git commit -m "Remove tire FX and wheel visuals (not visible in-car)"`.

---

### Task 3: Rewrite the driving model (≈1.5–2 h incl. play check)

**Files:** rewrite `CarController.cs`, `CarHandlingConfig.cs`; modify `CarInputReader.cs`, `BoostPostProcessing.cs`, `VehicleDebugInfoProvider.cs`; delete `CarDriveModel.cs`, `CarSteeringModel.cs`, `WheelForceModel.cs`, `Tests/EditMode/Editor/Vehicle/` (+ metas).

**Public surface of `CarController` after this task:** `SpeedMps`, `IsGrounded`, `InReverse`, `SteerAngleDegrees`, `OverdriveFactor`, `FrontGrip01`, `RearGrip01`, `FrontGripUsage01`, `RearGripUsage01`, `DriftAngle`.

- [ ] **Step 1: Delete the old helpers and their tests**: `CarDriveModel.cs`, `CarSteeringModel.cs`, `WheelForceModel.cs`, and the folder `Ralli/Assets/Tests/EditMode/Editor/Vehicle` (+ its `.meta`). The road tests stay.

- [ ] **Step 2: `CarInputReader.cs`**: rename `public bool Boost` to `public bool Overdrive`. In `Update`, use:

```csharp
        Overdrive = actions.Driving.Boost.IsPressed(); // Input action is still named "Boost".
```

- [ ] **Step 3: Replace `CarHandlingConfig.cs` entirely**

```csharp
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Ralli/Vehicle/Car Handling Config", fileName = "CarHandlingConfig")]
public class CarHandlingConfig : ScriptableObject
{
    [Header("Steering")]
    [Tooltip("Maximum front wheel steering angle in degrees.")]
    [Range(0f, 45f)] public float maxSteerAngle = 30f;
    [Tooltip("How quickly steering moves toward target input.")]
    public float steerResponse = 8f;
    [Tooltip("Forward speed (km/h) where high-speed steering reduction is fully applied.")]
    public float steerFadeSpeedKph = 162f;
    [Tooltip("Steering fraction kept at high speed. Lower = calmer at speed.")]
    [Range(0.2f, 1f)] public float highSpeedSteerFactor = 0.6f;
    [Tooltip("Steer input magnitude that counts as 'steering' for grip changes.")]
    [Range(0f, 1f)] public float steeringThreshold = 0.3f;

    [Header("Power")]
    [Tooltip("Forward acceleration at full throttle (m/s²).")]
    public float baseAcceleration = 5.5f;
    [Tooltip("Approximate top speed on normal throttle (km/h).")]
    public float maxSpeedKph = 150f;
    [Tooltip("Power multiplier with Overdrive fully in. Top speed scales by sqrt of this.")]
    public float overdrivePowerMultiplier = 1.6f;
    [Tooltip("How quickly Overdrive ramps in (per second).")]
    [FormerlySerializedAs("boostRampUpSpeed")] public float overdriveRampUpSpeed = 8f;
    [Tooltip("How quickly Overdrive ramps out (per second).")]
    [FormerlySerializedAs("boostRampDownSpeed")] public float overdriveRampDownSpeed = 6f;
    [Tooltip("Constant slowdown while rolling (m/s²).")]
    public float rollingDeceleration = 0.5f;
    [Tooltip("Deceleration at full brake (m/s²).")]
    public float brakeDeceleration = 10f;
    [Tooltip("Acceleration in reverse (m/s²).")]
    public float reverseAcceleration = 3f;
    [Tooltip("Reverse speed cap (km/h).")]
    public float reverseMaxSpeedKph = 25f;

    [Header("Grip")]
    [Tooltip("How hard each axle fights sideways sliding (1/s). Higher = crisper.")]
    public float gripStiffness = 12f;
    [Tooltip("Front axle max sideways grip in g.")]
    public float frontGripG = 1.15f;
    [Tooltip("Rear axle max sideways grip in g. Keep above front so the car pushes before it spins.")]
    public float rearGripG = 1.3f;
    [Tooltip("Front grip multiplier while steering on normal throttle (understeer).")]
    [Range(0f, 1f)] public float frontGripUnderThrottle = 0.8f;
    [Tooltip("Rear grip multiplier while steering with Overdrive (oversteer).")]
    [Range(0f, 1f)] public float rearGripInOverdrive = 0.45f;
    [Tooltip("Rear grip ceiling while handbrake is held. Applied instantly.")]
    [Range(0f, 1f)] public float handbrakeRearGrip = 0.3f;
    [Tooltip("Seconds for front grip to move fully between 1 and its lowered value.")]
    public float frontGripResponseTime = 0.4f;
    [Tooltip("Seconds for rear grip to move fully between 1 and its lowered value. Higher = longer drifts.")]
    public float rearGripResponseTime = 0.3f;

    [Header("Body")]
    [Tooltip("Target distance from car pivot to ground (m).")]
    public float rideHeight = 1.0f;
    [Tooltip("Extra probe length beyond ride height before the car counts as airborne (m).")]
    public float groundProbeExtra = 0.6f;
    [Tooltip("Ride height spring (1/s²). ~160 = 2 Hz bounce.")]
    public float supportStiffness = 160f;
    [Tooltip("Ride height damping (1/s).")]
    public float supportDamping = 15f;
    [Tooltip("How hard the body pitches/rolls to match the ground (1/s²).")]
    public float alignStiffness = 40f;
    [Tooltip("Pitch/roll damping while aligning (1/s).")]
    public float alignDamping = 8f;
    [Tooltip("Rigidbody center-of-mass Y offset.")]
    public float centerOfMassYOffset = -0.6f;
}
```

- [ ] **Step 4: Replace `CarController.cs` entirely**

```csharp
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CarInputReader))]
public class CarController : MonoBehaviour
{
    private const float MpsToKph = 3.6f;

    [Header("Config")]
    [SerializeField] private CarHandlingConfig handling;

    [Header("Geometry")]
    [Tooltip("Front axle distance ahead of the pivot (m).")]
    [SerializeField] private float frontAxleOffset = 1.3f;
    [Tooltip("Rear axle distance behind the pivot (m).")]
    [SerializeField] private float rearAxleOffset = 1.3f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = ~0;

    private Rigidbody rb;
    private CarInputReader input;
    private bool grounded;
    private float groundDistance;
    private Vector3 groundNormal = Vector3.up;
    private float steerAngle;
    private bool inReverse;
    private float overdriveFactor;
    private float frontGrip = 1f;
    private float rearGrip = 1f;
    private float frontGripUsage;
    private float rearGripUsage;

    public float SpeedMps => rb == null ? 0f : rb.linearVelocity.magnitude;
    public bool IsGrounded => grounded;
    public bool InReverse => inReverse;
    public float SteerAngleDegrees => steerAngle;
    public float OverdriveFactor => overdriveFactor;
    public float FrontGrip01 => frontGrip;
    public float RearGrip01 => rearGrip;
    public float FrontGripUsage01 => Mathf.Clamp01(frontGripUsage);
    public float RearGripUsage01 => Mathf.Clamp01(rearGripUsage);
    public float DriftAngle => GetDriftAngle();

    private void Awake()
    {
        if (handling == null)
        {
            handling = ScriptableObject.CreateInstance<CarHandlingConfig>();
        }

        rb = GetComponent<Rigidbody>();
        input = GetComponent<CarInputReader>();
        rb.centerOfMass = new Vector3(0f, handling.centerOfMassYOffset, 0f);
    }

    private void FixedUpdate()
    {
        if (handling == null)
        {
            return;
        }

        float deltaTime = Time.fixedDeltaTime;
        bool overdrive = input.Overdrive;
        float throttle = overdrive ? 1f : input.Throttle;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        ProbeGround();
        UpdateSteering(forwardSpeed, deltaTime);
        UpdateReverse(throttle, forwardSpeed);
        UpdateOverdrive(overdrive, deltaTime);
        UpdateGrip(overdrive, deltaTime);

        if (!grounded)
        {
            frontGripUsage = 0f;
            rearGripUsage = 0f;
            return;
        }

        ApplySupport();
        ApplyDrive(throttle, forwardSpeed, deltaTime);

        float frontShare = rearAxleOffset / (frontAxleOffset + rearAxleOffset);
        frontGripUsage = ApplyAxleGrip(frontAxleOffset, steerAngle, handling.frontGripG * frontGrip, frontShare);
        rearGripUsage = ApplyAxleGrip(-rearAxleOffset, 0f, handling.rearGripG * rearGrip, 1f - frontShare);
    }

    private void ProbeGround()
    {
        float probeLength = handling.rideHeight + handling.groundProbeExtra;
        grounded = Physics.Raycast(
            transform.position,
            -transform.up,
            out RaycastHit hit,
            probeLength,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
        groundDistance = grounded ? hit.distance : probeLength;
        groundNormal = grounded ? hit.normal : Vector3.up;
    }

    private void UpdateSteering(float forwardSpeed, float deltaTime)
    {
        float fade = Mathf.Clamp01(Mathf.Abs(forwardSpeed) * MpsToKph / Mathf.Max(1f, handling.steerFadeSpeedKph));
        float steerFactor = Mathf.Lerp(1f, handling.highSpeedSteerFactor, fade);
        float targetAngle = input.Steer * handling.maxSteerAngle * steerFactor;
        steerAngle = Mathf.MoveTowards(steerAngle, targetAngle, handling.steerResponse * handling.maxSteerAngle * deltaTime);
    }

    private void UpdateReverse(float throttle, float forwardSpeed)
    {
        if (!inReverse && input.Brake > 0.1f && forwardSpeed < 0.5f)
        {
            inReverse = true;
        }
        else if (inReverse && throttle > 0.1f && forwardSpeed > -0.5f)
        {
            inReverse = false;
        }
    }

    private void UpdateOverdrive(bool overdrive, float deltaTime)
    {
        float rate = overdrive ? handling.overdriveRampUpSpeed : handling.overdriveRampDownSpeed;
        overdriveFactor = Mathf.MoveTowards(overdriveFactor, overdrive ? 1f : 0f, rate * deltaTime);
    }

    private void UpdateGrip(bool overdrive, float deltaTime)
    {
        bool steering = Mathf.Abs(input.Steer) > handling.steeringThreshold;
        float frontTarget = steering && input.Throttle > 0.5f && !overdrive ? handling.frontGripUnderThrottle : 1f;
        float rearTarget = steering && overdrive ? handling.rearGripInOverdrive : 1f;

        frontGrip = Mathf.MoveTowards(frontGrip, frontTarget, deltaTime / Mathf.Max(0.01f, handling.frontGripResponseTime));
        rearGrip = Mathf.MoveTowards(rearGrip, rearTarget, deltaTime / Mathf.Max(0.01f, handling.rearGripResponseTime));

        if (input.Handbrake)
        {
            rearGrip = Mathf.Min(rearGrip, handling.handbrakeRearGrip);
        }
    }

    private void ApplySupport()
    {
        float compression = handling.rideHeight - groundDistance;
        float verticalSpeed = Vector3.Dot(rb.linearVelocity, transform.up);
        float lift = compression * handling.supportStiffness - verticalSpeed * handling.supportDamping;
        if (lift > 0f)
        {
            rb.AddForce(transform.up * lift, ForceMode.Acceleration);
        }

        Vector3 angularVelocity = rb.angularVelocity;
        Vector3 pitchRollRate = angularVelocity - transform.up * Vector3.Dot(angularVelocity, transform.up);
        Vector3 align = Vector3.Cross(transform.up, groundNormal) * handling.alignStiffness - pitchRollRate * handling.alignDamping;
        rb.AddTorque(align, ForceMode.Acceleration);
    }

    private void ApplyDrive(float throttle, float forwardSpeed, float deltaTime)
    {
        float speed = Mathf.Abs(forwardSpeed);
        float push;
        if (inReverse)
        {
            push = speed < handling.reverseMaxSpeedKph / MpsToKph ? -handling.reverseAcceleration * input.Brake : 0f;
        }
        else
        {
            push = handling.baseAcceleration * Mathf.Lerp(1f, handling.overdrivePowerMultiplier, overdriveFactor) * throttle;
        }

        float speed01 = speed / Mathf.Max(1f, handling.maxSpeedKph / MpsToKph);
        float brake = inReverse ? throttle : input.Brake;
        float slowdown = handling.rollingDeceleration
            + handling.baseAcceleration * speed01 * speed01
            + brake * handling.brakeDeceleration;
        // Never let slowdown alone push the car past zero.
        slowdown = Mathf.Min(slowdown, speed / deltaTime);

        rb.AddForce(transform.forward * (push - Mathf.Sign(forwardSpeed) * slowdown), ForceMode.Acceleration);
    }

    // Pushes the axle sideways against its slip, up to the axle's grip limit.
    // Returns demand / limit (> 1 means the axle is sliding).
    private float ApplyAxleGrip(float axleOffset, float steerDegrees, float gripG, float massShare)
    {
        Vector3 axlePoint = rb.worldCenterOfMass + transform.forward * axleOffset;
        Vector3 wheelRight = Quaternion.AngleAxis(steerDegrees, transform.up) * transform.right;
        float slipSpeed = Vector3.Dot(rb.GetPointVelocity(axlePoint), wheelRight);

        float axleMass = rb.mass * massShare;
        float wantedForce = -slipSpeed * handling.gripStiffness * axleMass;
        float maxForce = gripG * Physics.gravity.magnitude * axleMass;
        if (maxForce <= 0f)
        {
            return 0f;
        }

        rb.AddForceAtPosition(wheelRight * Mathf.Clamp(wantedForce, -maxForce, maxForce), axlePoint, ForceMode.Force);
        return Mathf.Abs(wantedForce) / maxForce;
    }

    private float GetDriftAngle()
    {
        if (rb == null)
        {
            return 0f;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude < 1f)
        {
            return 0f;
        }

        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        return Vector3.SignedAngle(flatForward.normalized, velocity.normalized, Vector3.up);
    }

    private void OnDrawGizmosSelected()
    {
        if (handling == null)
        {
            return;
        }

        Gizmos.color = grounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, transform.position - transform.up * (handling.rideHeight + handling.groundProbeExtra));
    }
}
```

- [ ] **Step 5: `BoostPostProcessing.cs`**: change `carController.BoostFactor` to `carController.OverdriveFactor`.

- [ ] **Step 6: `VehicleDebugInfoProvider.BuildDebugInfo`**: replace the body with:

```csharp
        builder.BeginSection("Vehicle");
        builder.AddFloat("Speed (km/h)", carController.SpeedMps * 3.6f);
        builder.AddBool("Grounded", carController.IsGrounded);
        builder.AddBool("In Reverse", carController.InReverse);
        builder.AddFloat("Steer Angle (deg)", carController.SteerAngleDegrees);
        builder.AddFloat("Drift Angle (deg)", carController.DriftAngle);
        builder.AddFloat("Overdrive", carController.OverdriveFactor);
        builder.AddFloat("Front Grip", carController.FrontGrip01);
        builder.AddFloat("Rear Grip", carController.RearGrip01);
        builder.AddFloat("Front Grip Usage", carController.FrontGripUsage01);
        builder.AddFloat("Rear Grip Usage", carController.RearGripUsage01);
        builder.AddFloat("Input Throttle", carInput.Throttle);
        builder.AddFloat("Input Brake", carInput.Brake);
        builder.AddFloat("Input Steer", carInput.Steer);
        builder.AddBool("Input Handbrake", carInput.Handbrake);
        builder.AddBool("Input Overdrive", carInput.Overdrive);
```

- [ ] **Step 7: Scene cleanup.** Under `Car`, delete the empty children `FrontLeft`, `FrontRight`, `RearLeft`, `RearRight` (the old wheel anchors). Reimport `ScriptableObjects/Vehicle/CarHandling_Default.asset`, then save the scene and the asset.

- [ ] **Step 8: Verify.** The console shows 0 errors. `grep -rnE "CarDriveModel|CarSteeringModel|WheelForceModel|BoostFactor|IsBoosting|\.Boost\b|TryGetWheel" Ralli/Assets/Scripts` returns only `actions.Driving.Boost` in `CarInputReader.cs`.

- [ ] **Step 9: Play check** with the debug overlay, keyboard only. Pass or fail each item:
  1. Idle: no creep or jitter, and Grounded stays true. Full steer at a standstill doesn't rotate the car.
  2. Full throttle reaches ~140 km/h. Adding Overdrive goes past 170 km/h.
  3. Throttle + steer: Front Grip falls toward 0.8 and the nose pushes wide. Lifting off tightens the line. Full lock at 100 km/h pushes wide, never spins.
  4. Overdrive + steer: the tail steps out, counter-steer catches it, and releasing recovers. Handbrake in a hairpin rotates the car immediately.
  5. Reverse with steer turns sensibly. Over a crest: no grip in the air, and a clean landing.

- [ ] **Step 10: Commit** — `git commit -m "Rewrite CarController as simple two-axle arcade grip model"`.

---

### Task 4: Engine heat (≈20 min)

**Files:** `CarHandlingConfig.cs`, `CarController.cs`, `VehicleDebugInfoProvider.cs`.

- [ ] **Step 1: Config.** Add after the Power block:

```csharp
    [Header("Engine Heat")]
    [Tooltip("Heat gained per second while Overdrive is held (0..1). 0.125 = 8 s from cold to max.")]
    public float heatRiseRate = 0.125f;
    [Tooltip("Heat lost per second while Overdrive is released.")]
    public float heatFallRate = 0.1f;
    [Tooltip("Heat level where power starts to taper.")]
    [Range(0f, 1f)] public float heatTaperStart = 0.8f;
    [Tooltip("Power multiplier at full heat.")]
    [Range(0f, 1f)] public float powerAtMaxHeat = 0.6f;
```

- [ ] **Step 2: Controller.**
  - Add the field `private float heat01;` and the property `public float Heat01 => heat01;`.
  - At the end of `UpdateOverdrive`, add:

    ```csharp
        float heatRate = overdrive ? handling.heatRiseRate : handling.heatFallRate;
        heat01 = Mathf.MoveTowards(heat01, overdrive ? 1f : 0f, heatRate * deltaTime);
    ```
  - In `ApplyDrive`'s forward branch, multiply `push` by the heat taper:

    ```csharp
            float heatPower = Mathf.Lerp(1f, handling.powerAtMaxHeat, Mathf.InverseLerp(handling.heatTaperStart, 1f, heat01));
            push = handling.baseAcceleration * Mathf.Lerp(1f, handling.overdrivePowerMultiplier, overdriveFactor) * heatPower * throttle;
    ```
- [ ] **Step 3: Debug.** Add `builder.AddFloat("Engine Heat", carController.Heat01);` after `"Overdrive"`.
- [ ] **Step 4: Verify.** The console shows 0 errors. Play: after holding Overdrive on a straight for ~8 s the car loses pull, and releasing it recovers.
- [ ] **Step 5: Commit** — `git commit -m "Add engine heat: Overdrive heats, power tapers near max"`.

---

### Task 5: Feel tuning (≈1–2 h, human in the loop)

**Files:** `CarHandling_Default.asset` (values). Also `CarHandlingConfig.cs` defaults, if the tuned values should become the new defaults.

Use the keyboard only and the debug overlay. Change one field at a time, and copy the value out before leaving Play mode.

- [ ] **Step 1: Speed band.** Aim for fun at ~100 km/h and dangerous at ~120 km/h on the generated road. Adjust `frontGripG` and `rearGripG` together, keeping rear 0.1–0.2 g above front.
- [ ] **Step 2: Throttle push.** Tune `frontGripUnderThrottle` and `frontGripResponseTime` until pushing wide is noticeable but lifting off recovers it.
- [ ] **Step 3: Overdrive drift.** Tune `rearGripInOverdrive` and `rearGripResponseTime` until Overdrive + steer starts a drift that counter-steer + release can catch. If slides feel too snappy or too floaty, adjust `gripStiffness` (8–20).
- [ ] **Step 4: Commit** — `git commit -m "Tune arcade handling for 100/120 km/h feel"`.

---

## After this plan

Next: road generation rework (`01-terrain-generation.md`). Then Phase 4 camera shake + head sway, adding G-force and impact outputs to `CarController` at that point. Then upgrades (3.3).
