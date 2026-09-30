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
    private bool engineRunning;
    private float ignitionTimer;
    private bool driveEnabled;
    private float drivePush;
    private float slideEntrySpeed;
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
    public bool EngineRunning => engineRunning;
    // Fired once, when the first throttle press turns the engine on.
    public event System.Action EngineStarted;

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
        engineRunning = !handling.startWithEngineOff;
        ignitionTimer = engineRunning ? handling.ignitionDriveDelay : 0f;
    }

    private void FixedUpdate()
    {
        if (handling == null)
        {
            return;
        }

        float deltaTime = Time.fixedDeltaTime;
        UpdateIgnition(deltaTime);
        // Overdrive only works on top of held throttle; alone it does nothing.
        bool overdrive = driveEnabled && input.Overdrive && input.Throttle > 0.5f;
        float throttle = driveEnabled ? input.Throttle : 0f;
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
        float spinCatch = GetSpinCatch();
        float rearGripNow = Mathf.Lerp(rearGrip * GetPowerOversteerFactor(), 1f, spinCatch);
        rearGripUsage = ApplyAxleGrip(-rearAxleOffset, 0f, handling.rearGripG * rearGripNow * exitGrip, 1f - frontShare);
        DampSpin(spinCatch, deltaTime);
        ApplyDriftMomentum(throttle);
    }

    // Engine push uses up part of the rear's grip budget (friction circle, scaled by powerOversteer),
    // so full power loosens the tail even at low speed where corners need little sideways grip.
    private float GetPowerOversteerFactor()
    {
        float rearGripAccel = Mathf.Max(0.1f, handling.rearGripG * Physics.gravity.magnitude);
        float used = Mathf.Clamp01(drivePush * handling.powerOversteer / rearGripAccel);
        return Mathf.Max(0.25f, Mathf.Sqrt(1f - used * used));
    }

    // 0 when gripping, 1 in a full slide (drift angle between the configured slide angles).
    private float GetSlideAmount()
    {
        return Mathf.InverseLerp(handling.driftMomentumAngles.x, handling.driftMomentumAngles.y, Mathf.Abs(GetDriftAngle()));
    }

    // Steering the same way the car is sliding (drift angle and steer share a sign) = counter-steer.
    private bool IsCounterSteering()
    {
        float drift = GetDriftAngle();
        return Mathf.Abs(drift) > 3f && Mathf.Abs(input.Steer) > handling.steeringThreshold && Mathf.Sign(drift) == Mathf.Sign(input.Steer);
    }

    // 0 below the drift angle limit, rising to 1 over the catch range. Off while the handbrake is
    // held, so handbrake turns can still rotate the car all the way round.
    private float GetSpinCatch()
    {
        if (input.Handbrake)
        {
            return 0f;
        }

        return Mathf.InverseLerp(handling.driftAngleLimit, handling.driftAngleLimit + Mathf.Max(1f, handling.spinCatchRange), Mathf.Abs(GetDriftAngle()));
    }

    // Past the limit, damp only the rotation that would deepen the slide; rotation back out is free.
    private void DampSpin(float spinCatch, float deltaTime)
    {
        if (spinCatch <= 0f)
        {
            return;
        }

        float drift = GetDriftAngle();
        float yawRate = Vector3.Dot(rb.angularVelocity, transform.up);
        // Turning right (positive yaw) reduces the drift angle, so deepening means opposite signs.
        if (Mathf.Sign(yawRate) == Mathf.Sign(drift))
        {
            return;
        }

        float keep = Mathf.Exp(-handling.spinYawDamping * spinCatch * deltaTime);
        rb.angularVelocity -= transform.up * (yawRate * (1f - keep));
    }

    // While sliding on throttle, top speed back up to what it was when the slide began, so drifts
    // carry their speed instead of bogging down, without ever speeding the car up.
    private void ApplyDriftMomentum(float throttle)
    {
        Vector3 travel = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up);
        float speed = travel.magnitude;
        float amount = GetSlideAmount();
        if (amount <= 0f)
        {
            slideEntrySpeed = speed;
            return;
        }

        float missing = slideEntrySpeed - speed;
        if (handling.driftMomentum <= 0f || throttle < 0.5f || inReverse || missing <= 0f || speed < 1f)
        {
            return;
        }

        float push = Mathf.Min(handling.driftMomentum * amount, missing / Time.fixedDeltaTime);
        rb.AddForce(travel / speed * push, ForceMode.Acceleration);
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
    // Switched off until the first throttle press; drive engages a moment after ignition.
    private void UpdateIgnition(float deltaTime)
    {
        if (!engineRunning && input.Throttle > 0.5f)
        {
            engineRunning = true;
            ignitionTimer = 0f;
            EngineStarted?.Invoke();
        }

        if (engineRunning)
        {
            ignitionTimer += deltaTime;
        }

        driveEnabled = engineRunning && ignitionTimer >= handling.ignitionDriveDelay;
    }

    private void UpdateEngineRpm(float throttle, float forwardSpeed, float deltaTime)
    {
        float target;
        float riseRate = handling.rpmRiseRate;
        if (!engineRunning)
        {
            target = 0f;
        }
        else if (grounded)
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
        // Overdrive loosens the rear as you steer into the turn (past the threshold, shaped by the
        // exponent). Counter-steering a slide gives the rear its grip back instead, so catching a
        // drift doesn't swing it into a spin the other way.
        float overdriveSteer = Mathf.InverseLerp(handling.steeringThreshold, 1f, Mathf.Abs(input.Steer));
        overdriveSteer = Mathf.Pow(overdriveSteer, handling.overdriveSteerExponent);
        if (IsCounterSteering())
        {
            overdriveSteer = 0f;
        }

        float rearTarget = overdrive ? Mathf.Lerp(1f, handling.rearGripInOverdrive, overdriveSteer) : 1f;

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
        if (!driveEnabled)
        {
            push = 0f;
        }
        else if (inReverse)
        {
            push = speed < handling.reverseMaxSpeedKph / MpsToKph ? -handling.reverseAcceleration * input.Brake : 0f;
        }
        else
        {
            float heatPower = Mathf.Lerp(1f, handling.powerAtMaxHeat, Mathf.InverseLerp(handling.heatTaperStart, 1f, heat01));
            push = handling.baseAcceleration * Mathf.Lerp(1f, handling.overdrivePowerMultiplier, overdriveFactor) * heatPower * throttle;
            push += handling.exitBoostAcceleration * ExitBoost01 * throttle * (1f - input.Brake);
            // Sideways, part of the power just spins the wheels instead of driving the car forward.
            push *= Mathf.Lerp(1f, handling.slidePowerFactor, GetSlideAmount());
        }

        drivePush = Mathf.Max(0f, push);

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
            + brake * handling.brakeDeceleration
            + (input.Handbrake ? handling.handbrakeDeceleration : 0f);
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
