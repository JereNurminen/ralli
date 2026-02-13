using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CarInputReader))]
public class CarController : MonoBehaviour
{
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

    public float SpeedMps => rb == null ? 0f : rb.linearVelocity.magnitude;
    public float SteerAngleDegrees => steerAngle;
    public float CurrentSteerFactor => currentSteerFactor;
    public int GroundedWheelCount => groundedWheels;
    public bool IsGrounded => groundedWheels > 0;
    public int WheelCount => 4;
    public float WheelRadius => handling == null ? 0.35f : handling.wheelRadius;

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

        SimulateWheel(frontLeft);
        SimulateWheel(frontRight);
        SimulateWheel(rearLeft);
        SimulateWheel(rearRight);

        // If front axle is unloaded, reduce steering authority to avoid violent snap when touching down.
        float frontGroundFactor = Mathf.Clamp01(groundedFrontWheels / 2f);
        float steerAuthority = Mathf.Lerp(handling.steerWhenFrontAirborne, 1f, frontGroundFactor);
        steerAngle *= steerAuthority;

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
        if (handling.steerFadeSpeed <= 0.01f)
        {
            return 1f;
        }

        float speed = Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.forward));
        float t = Mathf.Clamp01(speed / handling.steerFadeSpeed);
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
            return;
        }

        wheel.grounded = true;
        groundedWheels++;
        if (wheel.axle == Axle.Front)
        {
            groundedFrontWheels++;
        }

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

        rb.AddForceAtPosition(transform.up * wheel.springForce, hit.point, ForceMode.Force);

        Vector3 forward = wheel.axle == Axle.Front
            ? Quaternion.AngleAxis(steerAngle, transform.up) * transform.forward
            : transform.forward;
        Vector3 right = Vector3.Cross(transform.up, forward).normalized;
        Vector3 pointVelocity = rb.GetPointVelocity(hit.point);

        float forwardSpeed = Vector3.Dot(pointVelocity, forward);
        float lateralSpeed = Vector3.Dot(pointVelocity, right);

        float driveInput = input.Throttle;
        float brakeInput = input.Brake;
        float handbrakeInput = input.Handbrake ? 1f : 0f;

        float lateralGrip = wheel.axle == Axle.Front ? handling.frontLateralGrip : handling.rearLateralGrip;
        if (wheel.axle == Axle.Rear && handbrakeInput > 0f)
        {
            lateralGrip *= handling.rearGripWhileHandbrake;
        }
        if (wheel.axle == Axle.Rear && groundedFrontWheels == 0)
        {
            lateralGrip *= handling.rearGripWhenFrontAirborne;
        }

        float lateralForce = -lateralSpeed * lateralGrip * rb.mass;

        float driveForce = 0f;
        if (wheel.axle == Axle.Rear)
        {
            driveForce = driveInput * handling.maxDriveForce * handling.rearDriveBias * 0.5f;
            if (groundedFrontWheels == 0)
            {
                driveForce *= handling.rearDriveWhenFrontAirborne;
            }
        }
        else
        {
            driveForce = driveInput * handling.maxDriveForce * (1f - handling.rearDriveBias) * 0.5f;
        }

        float handbrakeAxleBias = wheel.axle == Axle.Rear ? handling.handbrakeRearBias : (1f - handling.handbrakeRearBias);
        float handbrakeForce = handbrakeInput * handling.handbrakeForce * handbrakeAxleBias;
        float brakeForce = brakeInput * handling.maxBrakeForce + handbrakeForce;
        float longitudinalForce = driveForce - Mathf.Sign(forwardSpeed) * brakeForce;

        Vector2 tireForce = new Vector2(lateralForce, longitudinalForce);
        float maxTireForce = wheel.springForce * handling.tireFriction;
        if (maxTireForce > 0f && tireForce.magnitude > maxTireForce)
        {
            tireForce = tireForce.normalized * maxTireForce;
        }

        Vector3 finalForce = right * tireForce.x + forward * tireForce.y;
        rb.AddForceAtPosition(finalForce, hit.point, ForceMode.Force);
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
        EnsureAnchor(frontLeft, new Vector3(-0.85f, -0.45f, 1.4f));
        EnsureAnchor(frontRight, new Vector3(0.85f, -0.45f, 1.4f));
        EnsureAnchor(rearLeft, new Vector3(-0.85f, -0.45f, -1.4f));
        EnsureAnchor(rearRight, new Vector3(0.85f, -0.45f, -1.4f));
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
}
