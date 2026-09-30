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
    [SerializeField] private float frontAxleOffset = 1.21f;
    [Tooltip("Rear axle distance behind the pivot (m).")]
    [SerializeField] private float rearAxleOffset = 1.21f;

    [Header("Impacts")]
    [Tooltip("Collisions that change the car's speed less than this (m/s) don't fire Impact.")]
    [SerializeField] private float minImpactSpeedChange = 1.5f;

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
    private float heat01;
    private float engineRpm01;
    private float overdriveHeldTime;
    private float exitBoostTimer;
    private float frontGrip = 1f;
    private float rearGrip = 1f;
    private float frontGripUsage;
    private float rearGripUsage;

    public float SpeedMps => rb == null ? 0f : rb.linearVelocity.magnitude;
    public bool IsGrounded => grounded;
    public bool InReverse => inReverse;
    public float SteerAngleDegrees => steerAngle;
    public float OverdriveFactor => overdriveFactor;
    public float Heat01 => heat01;
    // Fake engine RPM, 0 = off, 1 = limiter. Drives engine sound and the rev counter.
    public float EngineRpm01 => engineRpm01;
    public float ExitBoost01 => handling == null || handling.exitBoostDuration <= 0f ? 0f : exitBoostTimer / handling.exitBoostDuration;
    public float FrontGrip01 => frontGrip;
    public float RearGrip01 => rearGrip;
    public float FrontGripUsage01 => Mathf.Clamp01(frontGripUsage);
    public float RearGripUsage01 => Mathf.Clamp01(rearGripUsage);
    public float DriftAngle => GetDriftAngle();

    // Fired on a hard collision: speed change (m/s) and the world direction the car was pushed.
    public event System.Action<float, Vector3> Impact;

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
        // Overdrive only works on top of held throttle; alone it does nothing.
        bool overdrive = input.Overdrive && input.Throttle > 0.5f;
        float throttle = input.Throttle;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        ProbeGround();
        UpdateSteering(forwardSpeed, deltaTime);
        UpdateReverse(throttle, forwardSpeed);
        UpdateOverdrive(overdrive, deltaTime);
        UpdateGrip(overdrive, deltaTime);
        UpdateEngineRpm(throttle, forwardSpeed, deltaTime);

        if (!grounded)
        {
            frontGripUsage = 0f;
            rearGripUsage = 0f;
            return;
        }

        ApplySupport();
        ApplyDrive(throttle, forwardSpeed, deltaTime);

        float frontShare = rearAxleOffset / (frontAxleOffset + rearAxleOffset);
        float exitGrip = 1f + handling.exitBoostGrip * ExitBoost01;
        frontGripUsage = ApplyAxleGrip(frontAxleOffset, steerAngle, handling.frontGripG * frontGrip * exitGrip, frontShare);
        rearGripUsage = ApplyAxleGrip(-rearAxleOffset, 0f, handling.rearGripG * rearGrip * exitGrip, 1f - frontShare);
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

        float heatRate = overdrive ? handling.heatRiseRate : handling.heatFallRate;
        heat01 = Mathf.MoveTowards(heat01, overdrive ? 1f : 0f, heatRate * deltaTime);

        // Releasing Overdrive (while staying on throttle) after a proper hold gives a short push + grip to exit the corner.
        if (overdrive)
        {
            overdriveHeldTime += deltaTime;
            exitBoostTimer = 0f;
        }
        else
        {
            if (overdriveHeldTime >= handling.exitBoostMinHold && input.Throttle > 0.5f)
            {
                exitBoostTimer = handling.exitBoostDuration;
                rearGrip = 1f;
                if (grounded && !inReverse)
                {
                    rb.AddForce(transform.forward * (handling.exitBoostKickKph / MpsToKph), ForceMode.VelocityChange);
                }
            }

            overdriveHeldTime = 0f;
            exitBoostTimer = Mathf.Max(0f, exitBoostTimer - deltaTime);
        }
    }

    // No real drivetrain: on the ground RPM follows speed through fake gears; in the air the
    // wheels spin free, so throttle sends it to the limiter until the car lands again.
    private void UpdateEngineRpm(float throttle, float forwardSpeed, float deltaTime)
    {
        float target;
        float riseRate = handling.rpmRiseRate;
        if (grounded)
        {
            target = GetGearRpm01(Mathf.Abs(forwardSpeed) * MpsToKph)
                     + throttle * handling.throttleRpmBump
                     + overdriveFactor * handling.overdriveRpmBump;
        }
        else
        {
            target = throttle > 0.1f ? 1f : handling.idleRpm01;
            riseRate = handling.airborneRpmRiseRate;
        }

        float rate = target > engineRpm01 ? riseRate : handling.rpmFallRate;
        engineRpm01 = Mathf.MoveTowards(engineRpm01, Mathf.Clamp01(target), rate * deltaTime);
    }

    // Revs climb through each gear's speed span, then drop at the "shift".
    private float GetGearRpm01(float speedKph)
    {
        if (speedKph < 3f)
        {
            return handling.idleRpm01;
        }

        float span = Mathf.Max(1f, handling.gearSpanKph);
        int gear = Mathf.Min(Mathf.FloorToInt(speedKph / span), Mathf.Max(1, handling.gearCount) - 1);
        float inGear = Mathf.Clamp01((speedKph - gear * span) / span);
        return Mathf.Lerp(handling.idleRpm01 + 0.2f, 0.9f, inGear);
    }

    private void UpdateGrip(bool overdrive, float deltaTime)
    {
        bool steering = Mathf.Abs(input.Steer) > handling.steeringThreshold;
        float frontTarget = steering && input.Throttle > 0.5f && !overdrive ? handling.frontGripUnderThrottle : 1f;
        // Overdrive grip loss scales with how hard you steer past the threshold (squared, so small
        // analog corrections barely loosen the rear). Digital full lock still gets the full effect.
        float overdriveSteer = Mathf.InverseLerp(handling.steeringThreshold, 1f, Mathf.Abs(input.Steer));
        float rearTarget = overdrive ? Mathf.Lerp(1f, handling.rearGripInOverdrive, overdriveSteer * overdriveSteer) : 1f;

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
            float heatPower = Mathf.Lerp(1f, handling.powerAtMaxHeat, Mathf.InverseLerp(handling.heatTaperStart, 1f, heat01));
            push = handling.baseAcceleration * Mathf.Lerp(1f, handling.overdrivePowerMultiplier, overdriveFactor) * heatPower * throttle;
            push += handling.exitBoostAcceleration * ExitBoost01 * throttle * (1f - input.Brake);
        }

        // Cancel most of the uphill gravity pull so climbs don't bleed speed.
        float gravityAlongForward = Vector3.Dot(Physics.gravity, transform.forward);
        if (gravityAlongForward * Mathf.Sign(forwardSpeed) < 0f)
        {
            push -= gravityAlongForward * (1f - handling.uphillGravityScale);
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

    private void OnCollisionEnter(Collision collision)
    {
        float speedChange = collision.impulse.magnitude / Mathf.Max(1f, rb.mass);
        if (speedChange < minImpactSpeedChange || collision.contactCount == 0)
        {
            return;
        }

        Vector3 push = Vector3.zero;
        for (int i = 0; i < collision.contactCount; i++)
        {
            push += collision.GetContact(i).normal;
        }

        Impact?.Invoke(speedChange, push.normalized);
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
