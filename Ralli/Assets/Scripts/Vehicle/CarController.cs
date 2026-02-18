using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CarInputReader))]
public class CarController : MonoBehaviour
{
    private const float MpsToKph = 3.6f;

    public struct WheelVisualState
    {
        public Vector3 AnchorPosition;
        public Vector3 SuspensionUp;
        public Vector3 Forward;
        public float SuspensionLength;
        public float Radius;
        public float SteerAngleDegrees;
        public bool Grounded;
    }

    public struct WheelTelemetry
    {
        public string Name;
        public bool Grounded;
        public float SpringForce;
        public float LateralForce;
        public float LongitudinalForce;
        public float MaxTireForce;
        public Vector3 AppliedForceWorld;
        public Vector3 ContactPoint;
        public Vector3 ContactNormal;
        public Vector3 Forward;
        public Vector3 Right;
    }

    private enum Axle
    {
        Front,
        Rear
    }

    [System.Serializable]
    private class Wheel
    {
        public string name;
        public Axle axle;
        public Transform anchor;

        [HideInInspector] public bool grounded;
        [HideInInspector] public float springLength;
        [HideInInspector] public float springVelocity;
        [HideInInspector] public float springForce;
        [HideInInspector] public float lastLateralForce;
        [HideInInspector] public float lastLongitudinalForce;
        [HideInInspector] public float lastMaxTireForce;
        [HideInInspector] public Vector3 lastAppliedForceWorld;
        [HideInInspector] public Vector3 lastContactPoint;
        [HideInInspector] public Vector3 lastContactNormal;
        [HideInInspector] public Vector3 lastForward;
        [HideInInspector] public Vector3 lastRight;
    }

    [Header("Config")]
    [SerializeField] private CarHandlingConfig handling;

    [Header("Wheel Anchors")]
    [SerializeField] private Wheel frontLeft = new Wheel { name = "FrontLeft", axle = Axle.Front };
    [SerializeField] private Wheel frontRight = new Wheel { name = "FrontRight", axle = Axle.Front };
    [SerializeField] private Wheel rearLeft = new Wheel { name = "RearLeft", axle = Axle.Rear };
    [SerializeField] private Wheel rearRight = new Wheel { name = "RearRight", axle = Axle.Rear };

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = ~0;

    private Rigidbody rb;
    private CarInputReader input;
    private Wheel[] wheels;
    private float steerAngle;
    private int groundedWheels;
    private int groundedFrontWheels;
    private float currentSteerFactor = 1f;
    private int currentFakeGear;
    private float currentFakeRpm01;
    private float shiftPauseTimer;
    private float baseDriveForceNow;
    private float baseEngineBrakingForceNow;
    private float boostFactor;
    private float nearZeroSteerTime;
    private bool inReverse;

    public float SpeedMps => rb == null ? 0f : rb.linearVelocity.magnitude;
    public bool InReverse => inReverse;
    public float SteerAngleDegrees => steerAngle;
    public float CurrentSteerFactor => currentSteerFactor;
    public int CurrentFakeGear => currentFakeGear + 1;
    public float FakeRpm01 => currentFakeRpm01;
    public int GroundedWheelCount => groundedWheels;
    public bool IsGrounded => groundedWheels > 0;
    public int WheelCount => 4;
    public float WheelRadius => handling == null ? 0.35f : handling.wheelRadius;
    public bool IsBoosting => boostFactor > 0.01f;
    public float BoostFactor => boostFactor;

    private void Awake()
    {
        EnsureWheelAnchors();

        if (handling == null)
        {
            handling = ScriptableObject.CreateInstance<CarHandlingConfig>();
        }

        rb = GetComponent<Rigidbody>();
        input = GetComponent<CarInputReader>();
        wheels = new[] { frontLeft, frontRight, rearLeft, rearRight };

        rb.centerOfMass = new Vector3(0f, handling.centerOfMassYOffset, 0f);

        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i].springLength = handling.suspensionRestLength;
            wheels[i].springVelocity = 0f;
            wheels[i].springForce = 0f;
        }
    }

    private void Reset()
    {
        EnsureWheelAnchors();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureWheelAnchors();
        }
    }

    private void FixedUpdate()
    {
        if (handling == null)
        {
            return;
        }

        groundedWheels = 0;
        groundedFrontWheels = 0;
        currentSteerFactor = EvaluateSteerFactor();
        float targetSteerAngle = input.Steer * handling.maxSteerAngle * currentSteerFactor;
        steerAngle = Mathf.MoveTowards(
            steerAngle,
            targetSteerAngle,
            handling.steerResponse * handling.maxSteerAngle * Time.fixedDeltaTime
        );
        if (Mathf.Abs(input.Steer) < 0.001f && Mathf.Abs(steerAngle) < 0.05f)
        {
            steerAngle = 0f;
        }

        int frontGroundedCountNow = GetFrontGroundedCountNow();
        float frontGroundFactor = Mathf.Clamp01(frontGroundedCountNow / 2f);
        float steerAuthority = Mathf.Lerp(handling.steerWhenFrontAirborne, 1f, frontGroundFactor);
        steerAngle *= steerAuthority;

        float forwardSpeedSigned = Vector3.Dot(rb.linearVelocity, transform.forward);
        bool nearlyStoppedOrReversing = forwardSpeedSigned < 0.5f;
        bool nearlyStoppedOrForward = forwardSpeedSigned > -0.5f;
        if (!inReverse && input.Brake > 0.1f && nearlyStoppedOrReversing)
        {
            inReverse = true;
        }
        else if (inReverse && input.Throttle > 0.1f && nearlyStoppedOrForward)
        {
            inReverse = false;
        }

        UpdateDriveState();

        float boostTarget = input.Boost ? 1f : 0f;
        float boostRate = input.Boost ? handling.boostRampUpSpeed : handling.boostRampDownSpeed;
        boostFactor = Mathf.MoveTowards(boostFactor, boostTarget, boostRate * Time.fixedDeltaTime);

        SimulateWheel(frontLeft);
        SimulateWheel(frontRight);
        SimulateWheel(rearLeft);
        SimulateWheel(rearRight);

        ApplyAntiRoll(frontLeft, frontRight);
        ApplyAntiRoll(rearLeft, rearRight);

        if (groundedWheels > 0)
        {
            ApplyPassiveResistance();
            ApplyAngularDamping();
            ApplyStraighteningAssist();
        }
    }

    private float EvaluateSteerFactor()
    {
        if (handling.steerFadeSpeedKph <= 0.01f)
        {
            return 1f;
        }

        float speedKph = Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.forward)) * MpsToKph;
        float t = Mathf.Clamp01(speedKph / handling.steerFadeSpeedKph);
        return Mathf.Lerp(1f, handling.highSpeedSteerFactor, t);
    }

    private void SimulateWheel(Wheel wheel)
    {
        if (wheel.anchor == null)
        {
            return;
        }

        float suspensionDistance = handling.suspensionRestLength + handling.wheelRadius;
        Vector3 rayOrigin = wheel.anchor.position;

        if (!Physics.Raycast(rayOrigin, -transform.up, out RaycastHit hit, suspensionDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            wheel.grounded = false;
            wheel.springForce = 0f;
            wheel.springLength = handling.suspensionRestLength;
            wheel.springVelocity = 0f;
            wheel.lastLateralForce = 0f;
            wheel.lastLongitudinalForce = 0f;
            wheel.lastMaxTireForce = 0f;
            wheel.lastAppliedForceWorld = Vector3.zero;
            wheel.lastContactPoint = rayOrigin - transform.up * suspensionDistance;
            wheel.lastContactNormal = transform.up;
            wheel.lastForward = transform.forward;
            wheel.lastRight = transform.right;
            return;
        }

        wheel.grounded = true;
        groundedWheels++;
        if (wheel.axle == Axle.Front)
        {
            groundedFrontWheels++;
        }
        int rearGroundedCount = GetRearGroundedCountNow();

        float wheelTravel = hit.distance - handling.wheelRadius;
        float springLengthNow = Mathf.Clamp(wheelTravel, 0f, handling.suspensionRestLength);
        float compression = handling.suspensionRestLength - springLengthNow;
        float springVelocity = (wheel.springLength - springLengthNow) / Time.fixedDeltaTime;
        springVelocity = Mathf.Clamp(springVelocity, -handling.maxSuspensionVelocity, handling.maxSuspensionVelocity);
        float springForce = compression * handling.springStrength + springVelocity * handling.damperStrength;
        float staticWheelLoad = rb.mass * Physics.gravity.magnitude * 0.25f;
        float maxSpringForce = staticWheelLoad * handling.maxSuspensionLoadFactor;

        wheel.springLength = springLengthNow;
        wheel.springVelocity = springVelocity;
        wheel.springForce = Mathf.Clamp(springForce, 0f, maxSpringForce);
        if (wheel.axle == Axle.Rear && rearGroundedCount == 1)
        {
            wheel.springForce *= handling.rearSpringForceWhenSingleRearWheelGrounded;
        }

        rb.AddForceAtPosition(transform.up * wheel.springForce, hit.point, ForceMode.Force);

        Vector3 forward = wheel.axle == Axle.Front
            ? Quaternion.AngleAxis(steerAngle, transform.up) * transform.forward
            : transform.forward;
        Vector3 right = Vector3.Cross(transform.up, forward).normalized;
        Vector3 pointVelocity = rb.GetPointVelocity(hit.point);

        float forwardSpeed = Vector3.Dot(pointVelocity, forward);
        float lateralSpeed = Vector3.Dot(pointVelocity, right);

        float driveInput = inReverse ? input.Brake : input.Throttle;
        float brakeInput = inReverse ? input.Throttle : input.Brake;
        float driveDirection = inReverse ? -1f : 1f;
        float handbrakeInput = input.Handbrake ? 1f : 0f;

        // --- Lateral force via slip curve ---
        float lateralGrip = wheel.axle == Axle.Front ? handling.frontLateralGrip : handling.rearLateralGrip;
        if (wheel.axle == Axle.Rear && handbrakeInput > 0f)
        {
            lateralGrip *= handling.rearGripWhileHandbrake;
        }
        if (wheel.axle == Axle.Rear && rearGroundedCount == 1)
        {
            lateralGrip *= handling.rearGripWhenSingleRearWheelGrounded;
        }
        if (wheel.axle == Axle.Rear && groundedFrontWheels == 0)
        {
            lateralGrip *= handling.rearGripWhenFrontAirborne;
        }

        float absLateralSlip = Mathf.Abs(lateralSpeed);
        float lateralSlipNormalized = absLateralSlip * handling.lateralSlipScale;
        float lateralGripFactor = handling.lateralSlipCurve != null
            ? Mathf.Clamp01(handling.lateralSlipCurve.Evaluate(lateralSlipNormalized))
            : 1f;
        float lateralForce = -Mathf.Sign(lateralSpeed) * lateralGripFactor * lateralGrip * wheel.springForce;

        // --- Longitudinal force (drive + brake) ---
        float driveForce = 0f;
        float engineBrakingForce = 0f;
        if (wheel.axle == Axle.Rear)
        {
            float rearBoostMultiplier = 1f + boostFactor * (handling.boostForceMultiplier - 1f);
            driveForce = driveDirection * driveInput * baseDriveForceNow * rearBoostMultiplier * handling.rearDriveBias * 0.5f;
            engineBrakingForce = baseEngineBrakingForceNow * handling.rearDriveBias * 0.5f;
            if (rearGroundedCount == 1)
            {
                driveForce *= handling.rearDriveWhenSingleRearWheelGrounded;
            }
            if (groundedFrontWheels == 0)
            {
                driveForce *= handling.rearDriveWhenFrontAirborne;
            }
        }
        else
        {
            driveForce = driveDirection * driveInput * baseDriveForceNow * (1f - handling.rearDriveBias) * 0.5f;
            engineBrakingForce = baseEngineBrakingForceNow * (1f - handling.rearDriveBias) * 0.5f;
        }

        float handbrakeAxleBias = wheel.axle == Axle.Rear ? handling.handbrakeRearBias : (1f - handling.handbrakeRearBias);
        float handbrakeForce = handbrakeInput * handling.handbrakeForce * handbrakeAxleBias;
        float brakeForce = brakeInput * handling.maxBrakeForce + handbrakeForce;
        float longitudinalForce = driveForce - Mathf.Sign(forwardSpeed) * brakeForce;
        if (Mathf.Abs(forwardSpeed) > 0.1f && driveInput <= 0.01f && brakeInput <= 0.01f && handbrakeInput <= 0.01f)
        {
            longitudinalForce -= Mathf.Sign(forwardSpeed) * engineBrakingForce;
        }

        // --- Friction circle clamp ---
        // When boosting, prioritise longitudinal force — lateral gets only the remaining budget.
        // This makes boost actively reduce rear grip → oversteer.
        Vector2 tireForce = new Vector2(lateralForce, longitudinalForce);
        float maxTireForce = wheel.springForce * handling.tireFriction;
        if (wheel.axle == Axle.Rear && rearGroundedCount == 1)
        {
            maxTireForce *= handling.rearTireForceCapWhenSingleRearWheelGrounded;
        }
        if (maxTireForce <= 0f)
        {
            tireForce = Vector2.zero;
        }
        else if (tireForce.magnitude > maxTireForce)
        {
            if (wheel.axle == Axle.Rear && boostFactor > 0.01f)
            {
                // Longitudinal-priority clamp: drive force eats the budget first,
                // lateral gets whatever is left. More boost = less rear grip.
                float absLong = Mathf.Abs(tireForce.y);
                float clampedLong = Mathf.Min(absLong, maxTireForce);
                float lateralBudget = Mathf.Sqrt(Mathf.Max(0f, maxTireForce * maxTireForce - clampedLong * clampedLong));
                // Blend between normal (ratio-preserving) and longitudinal-priority based on boost factor.
                Vector2 normalClamped = tireForce.normalized * maxTireForce;
                Vector2 boostClamped = new Vector2(
                    Mathf.Sign(tireForce.x) * Mathf.Min(Mathf.Abs(tireForce.x), lateralBudget),
                    Mathf.Sign(tireForce.y) * clampedLong
                );
                tireForce = Vector2.Lerp(normalClamped, boostClamped, boostFactor);
            }
            else
            {
                tireForce = tireForce.normalized * maxTireForce;
            }
        }

        Vector3 finalForce = right * tireForce.x + forward * tireForce.y;
        rb.AddForceAtPosition(finalForce, hit.point, ForceMode.Force);

        wheel.lastLateralForce = tireForce.x;
        wheel.lastLongitudinalForce = tireForce.y;
        wheel.lastMaxTireForce = maxTireForce;
        wheel.lastAppliedForceWorld = finalForce;
        wheel.lastContactPoint = hit.point;
        wheel.lastContactNormal = hit.normal;
        wheel.lastForward = forward;
        wheel.lastRight = right;
    }

    private void ApplyAntiRoll(Wheel leftWheel, Wheel rightWheel)
    {
        if (handling.antiRollStiffness <= 0f)
        {
            return;
        }

        if (!leftWheel.grounded || !rightWheel.grounded)
        {
            return;
        }

        float leftTravel = GetSuspensionTravel01(leftWheel);
        float rightTravel = GetSuspensionTravel01(rightWheel);
        float antiRollForce = (leftTravel - rightTravel) * handling.antiRollStiffness;

        if (leftWheel.anchor != null)
        {
            rb.AddForceAtPosition(-transform.up * antiRollForce, leftWheel.anchor.position, ForceMode.Force);
        }

        if (rightWheel.anchor != null)
        {
            rb.AddForceAtPosition(transform.up * antiRollForce, rightWheel.anchor.position, ForceMode.Force);
        }
    }

    private float GetSuspensionTravel01(Wheel wheel)
    {
        if (!wheel.grounded || handling.suspensionRestLength <= 0.001f)
        {
            return 1f;
        }

        return Mathf.Clamp01(wheel.springLength / handling.suspensionRestLength);
    }

    private void ApplyPassiveResistance()
    {
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;
        if (speed < 0.001f)
        {
            return;
        }

        if (speed < 0.05f && input.Throttle <= 0.02f && input.Brake <= 0.02f)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        Vector3 dragDirection = -velocity.normalized;
        float resistance = handling.rollingResistance + handling.aerodynamicDrag * speed * speed;
        rb.AddForce(dragDirection * resistance, ForceMode.Force);
    }

    private void UpdateDriveState()
    {
        float speedKph = Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.forward)) * MpsToKph;
        float topSpeedKph = Mathf.Max(1f, handling.fakeGearTopSpeedKph);
        float normalizedSpeed = Mathf.Clamp01(speedKph / topSpeedKph);

        int gearCount = Mathf.Max(1, handling.fakeGearCount);
        int gearIndex = GetGearIndex(normalizedSpeed, gearCount, handling.fakeGearThresholds01);
        float rpm01 = GetGearBandProgress(normalizedSpeed, gearIndex, gearCount, handling.fakeGearThresholds01);

        if (gearIndex != currentFakeGear)
        {
            currentFakeGear = gearIndex;
            shiftPauseTimer = Mathf.Max(0.01f, handling.shiftPauseDuration);
        }

        currentFakeRpm01 = rpm01;
        if (shiftPauseTimer > 0f)
        {
            shiftPauseTimer = Mathf.Max(0f, shiftPauseTimer - Time.fixedDeltaTime);
        }

        float curveMultiplier = 1f;
        if (handling.baseDriveForceBySpeedKph != null)
        {
            curveMultiplier = Mathf.Max(0f, handling.baseDriveForceBySpeedKph.Evaluate(speedKph));
        }

        // Per-gear S-curve taper: strongest after shift, softer near redline.
        float gearShape = Mathf.SmoothStep(1.12f, 0.82f, rpm01);
        float shiftMultiplier = 1f;
        if (shiftPauseTimer > 0f && handling.shiftPauseDuration > 0f)
        {
            float shiftT = 1f - (shiftPauseTimer / handling.shiftPauseDuration);
            float dip = Mathf.Clamp01(handling.shiftTorqueDip);
            shiftMultiplier = Mathf.Lerp(1f - dip, 1f, shiftT);
        }

        baseDriveForceNow = handling.maxDriveForce * curveMultiplier * gearShape * shiftMultiplier;
        baseEngineBrakingForceNow = handling.maxDriveForce * Mathf.Clamp01(handling.engineBrakingStrength) * rpm01;
    }

    private static int GetGearIndex(float normalizedSpeed, int gearCount, float[] thresholds01)
    {
        if (gearCount <= 1)
        {
            return 0;
        }

        for (int i = 0; i < gearCount - 1; i++)
        {
            float threshold = GetThreshold(i, gearCount, thresholds01);
            if (normalizedSpeed < threshold)
            {
                return i;
            }
        }

        return gearCount - 1;
    }

    private static float GetGearBandProgress(float normalizedSpeed, int gearIndex, int gearCount, float[] thresholds01)
    {
        if (gearCount <= 1)
        {
            return Mathf.Clamp01(normalizedSpeed);
        }

        float bandStart = gearIndex <= 0 ? 0f : GetThreshold(gearIndex - 1, gearCount, thresholds01);
        float bandEnd = gearIndex >= gearCount - 1 ? 1f : GetThreshold(gearIndex, gearCount, thresholds01);
        float bandLength = Mathf.Max(0.0001f, bandEnd - bandStart);
        return Mathf.Clamp01((normalizedSpeed - bandStart) / bandLength);
    }

    private static float GetThreshold(int index, int gearCount, float[] thresholds01)
    {
        if (thresholds01 == null || thresholds01.Length < gearCount - 1)
        {
            return (index + 1) / (float)gearCount;
        }

        float minAllowed = index <= 0 ? 0f : Mathf.Clamp01(thresholds01[index - 1]);
        float maxAllowed = index >= thresholds01.Length - 1 ? 1f : Mathf.Clamp01(thresholds01[index + 1]);
        float threshold = Mathf.Clamp01(thresholds01[index]);
        return Mathf.Clamp(threshold, minAllowed, maxAllowed);
    }

    private void ApplyAngularDamping()
    {
        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        localAngularVelocity.y *= 1f / (1f + handling.yawDamping * Time.fixedDeltaTime);
        localAngularVelocity.z *= 1f / (1f + handling.rollDamping * Time.fixedDeltaTime);
        rb.angularVelocity = transform.TransformDirection(localAngularVelocity);
    }

    private void ApplyStraighteningAssist()
    {
        if (Mathf.Abs(input.Steer) > handling.straighteningSteerThreshold)
        {
            nearZeroSteerTime = 0f;
            return;
        }

        nearZeroSteerTime += Time.fixedDeltaTime;
        if (nearZeroSteerTime < handling.straighteningActivationDelay)
        {
            return;
        }

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        localVelocity.x *= 1f / (1f + handling.straighteningLateralDamping * Time.fixedDeltaTime);
        rb.linearVelocity = transform.TransformDirection(localVelocity);

        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        localAngularVelocity.y *= 1f / (1f + handling.straighteningYawDamping * Time.fixedDeltaTime);
        rb.angularVelocity = transform.TransformDirection(localAngularVelocity);
    }

    private void EnsureWheelAnchors()
    {
        // Approximate hot-hatch footprint: ~1.58m track, ~2.60m wheelbase.
        EnsureAnchor(frontLeft, new Vector3(-0.79f, -0.36f, 1.30f));
        EnsureAnchor(frontRight, new Vector3(0.79f, -0.36f, 1.30f));
        EnsureAnchor(rearLeft, new Vector3(-0.79f, -0.36f, -1.30f));
        EnsureAnchor(rearRight, new Vector3(0.79f, -0.36f, -1.30f));
    }

    private void EnsureAnchor(Wheel wheel, Vector3 localPosition)
    {
        if (wheel.anchor != null)
        {
            SetAnchorLocalPosition(wheel.anchor, localPosition);
            return;
        }

        Transform existing = transform.Find(wheel.name);
        if (existing != null)
        {
            wheel.anchor = existing;
            SetAnchorLocalPosition(existing, localPosition);
            return;
        }

        GameObject anchorObject = new GameObject(wheel.name);
        anchorObject.transform.SetParent(transform, false);
        SetAnchorLocalPosition(anchorObject.transform, localPosition);
        anchorObject.transform.localRotation = Quaternion.identity;

        wheel.anchor = anchorObject.transform;
    }

    private void SetAnchorLocalPosition(Transform anchor, Vector3 desiredLocalPosition)
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) < 0.0001f ? 1f : scale.x;
        scale.y = Mathf.Abs(scale.y) < 0.0001f ? 1f : scale.y;
        scale.z = Mathf.Abs(scale.z) < 0.0001f ? 1f : scale.z;

        anchor.localPosition = new Vector3(
            desiredLocalPosition.x / scale.x,
            desiredLocalPosition.y / scale.y,
            desiredLocalPosition.z / scale.z
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (handling == null)
        {
            return;
        }

        Wheel[] gizmoWheels = { frontLeft, frontRight, rearLeft, rearRight };
        Gizmos.color = Color.yellow;

        foreach (Wheel wheel in gizmoWheels)
        {
            if (wheel.anchor == null)
            {
                continue;
            }

            Vector3 start = wheel.anchor.position;
            Vector3 end = start - transform.up * (handling.suspensionRestLength + handling.wheelRadius);
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(end, handling.wheelRadius);
        }
    }

    public bool TryGetWheelVisualState(int wheelIndex, out WheelVisualState state)
    {
        state = default;
        if (wheels == null || handling == null)
        {
            return false;
        }

        if (wheelIndex < 0 || wheelIndex >= wheels.Length)
        {
            return false;
        }

        Wheel wheel = wheels[wheelIndex];
        if (wheel.anchor == null)
        {
            return false;
        }

        Vector3 forward = wheel.axle == Axle.Front
            ? Quaternion.AngleAxis(steerAngle, transform.up) * transform.forward
            : transform.forward;

        state = new WheelVisualState
        {
            AnchorPosition = wheel.anchor.position,
            SuspensionUp = transform.up,
            Forward = forward.normalized,
            SuspensionLength = wheel.springLength,
            Radius = handling.wheelRadius,
            SteerAngleDegrees = wheel.axle == Axle.Front ? steerAngle : 0f,
            Grounded = wheel.grounded
        };

        return true;
    }

    public bool TryGetWheelTelemetry(int wheelIndex, out WheelTelemetry telemetry)
    {
        telemetry = default;
        if (wheels == null || wheelIndex < 0 || wheelIndex >= wheels.Length)
        {
            return false;
        }

        Wheel wheel = wheels[wheelIndex];
        telemetry = new WheelTelemetry
        {
            Name = wheel.name,
            Grounded = wheel.grounded,
            SpringForce = wheel.springForce,
            LateralForce = wheel.lastLateralForce,
            LongitudinalForce = wheel.lastLongitudinalForce,
            MaxTireForce = wheel.lastMaxTireForce,
            AppliedForceWorld = wheel.lastAppliedForceWorld,
            ContactPoint = wheel.lastContactPoint,
            ContactNormal = wheel.lastContactNormal,
            Forward = wheel.lastForward,
            Right = wheel.lastRight
        };

        return true;
    }

    private int GetRearGroundedCountNow()
    {
        float suspensionDistance = handling.suspensionRestLength + handling.wheelRadius;
        int count = 0;
        if (IsAnchorGrounded(rearLeft.anchor, suspensionDistance))
        {
            count++;
        }

        if (IsAnchorGrounded(rearRight.anchor, suspensionDistance))
        {
            count++;
        }

        return count;
    }

    private int GetFrontGroundedCountNow()
    {
        float suspensionDistance = handling.suspensionRestLength + handling.wheelRadius;
        int count = 0;
        if (IsAnchorGrounded(frontLeft.anchor, suspensionDistance))
        {
            count++;
        }

        if (IsAnchorGrounded(frontRight.anchor, suspensionDistance))
        {
            count++;
        }

        return count;
    }

    private bool IsAnchorGrounded(Transform anchor, float suspensionDistance)
    {
        if (anchor == null)
        {
            return false;
        }

        return Physics.Raycast(
            anchor.position,
            -transform.up,
            suspensionDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }
}
