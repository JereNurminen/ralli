using UnityEngine;

// Driver's-eye camera. The eye sits at a fixed point in the car; on top of that:
// - Head sway: a spring-damped head position pushed around by the car's acceleration (corners,
//   braking, bumps, landings). Position only; the view never tilts from normal driving.
// - Subtle road vibration that grows with speed (position only).
// - A violent head slam on hard impacts, with its own spring, a nod/roll and shake.
// - Looking at the road: the view turns toward a point on the road ahead (into corners, and back
//   toward the road when sliding), fading out in spins so it never swings wildly. Plus manual
//   left/right look.
// Everything is tuned for feel, not realism.
[RequireComponent(typeof(Camera))]
public class InCarCamera : MonoBehaviour
{
    private const float Gravity = 9.81f;

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

    [Header("Head Sway (position only)")]
    [Tooltip("How far the head moves per g of car acceleration (m): x = sideways, y = up/down, z = forward/back.")]
    [SerializeField] private Vector3 swayPerG = new Vector3(0.012f, 0.008f, 0.015f);
    [Tooltip("Sway travel limit per axis (m).")]
    [SerializeField] private Vector3 maxSwayOffset = new Vector3(0.03f, 0.02f, 0.035f);
    [Tooltip("Sway spring frequency (Hz). Lower = floppier.")]
    [SerializeField] private float swayFrequency = 1.6f;
    [Tooltip("Sway spring damping. 1 = no bounce, lower = wobblier.")]
    [Range(0.05f, 1.5f)] [SerializeField] private float swayDamping = 0.9f;
    [Tooltip("Smoothing of the car acceleration fed to the sway (s). Higher = calmer, filters physics jitter.")]
    [SerializeField] private float swayAccelerationSmoothing = 0.2f;
    [Tooltip("Acceleration above this (g) is ignored by the sway; hard hits go through the impact slam instead.")]
    [SerializeField] private float maxSwayG = 1.5f;

    [Header("Road Vibration (position only)")]
    [Tooltip("Head jitter at full vibration speed (m). 0 = off.")]
    [SerializeField] private float roadVibrationAmount = 0.0008f;
    [Tooltip("Jitter speed. Lower = slower wobble.")]
    [SerializeField] private float roadVibrationFrequency = 6f;
    [Tooltip("Speed (km/h) where vibration reaches full strength.")]
    [SerializeField] private float vibrationFullSpeedKph = 160f;

    [Header("Impact Slam")]
    [Tooltip("Head velocity (m/s) thrown opposite the push, per 1 m/s of impact speed change.")]
    [SerializeField] private float slamPerImpactSpeed = 0.35f;
    [Tooltip("Cap on the slam head velocity (m/s).")]
    [SerializeField] private float maxSlamVelocity = 4f;
    [Tooltip("Slam travel limit per axis (m).")]
    [SerializeField] private Vector3 maxSlamOffset = new Vector3(0.1f, 0.08f, 0.16f);
    [Tooltip("Slam spring frequency (Hz): how fast the head comes back.")]
    [SerializeField] private float slamFrequency = 3f;
    [Tooltip("Slam spring damping. Lower = more rebound.")]
    [Range(0.05f, 1.5f)] [SerializeField] private float slamDamping = 0.45f;
    [Tooltip("Head nod (degrees) per meter of forward/back slam.")]
    [SerializeField] private float slamNodPerMeter = 70f;
    [Tooltip("Head roll (degrees) per meter of sideways slam.")]
    [SerializeField] private float slamRollPerMeter = 50f;
    [Tooltip("Shake added per 1 m/s of impact speed change (shake is 0..1).")]
    [SerializeField] private float shakePerImpactSpeed = 0.08f;
    [Tooltip("How fast shake dies out (per second).")]
    [SerializeField] private float shakeDecay = 1.6f;
    [Tooltip("Position shake at full shake (m).")]
    [SerializeField] private float shakeAmount = 0.04f;
    [Tooltip("Rotation shake at full shake (degrees).")]
    [SerializeField] private float shakeRotation = 5f;
    [SerializeField] private float shakeFrequency = 22f;

    [Header("Look At Road")]
    [Tooltip("Share of the angle to the road point ahead the view turns toward (0 = off, 1 = look straight at it).")]
    [Range(0f, 1f)] [SerializeField] private float roadLookFollow = 0.6f;
    [Tooltip("Max turn toward the road (degrees).")]
    [SerializeField] private float maxRoadLook = 20f;
    [Tooltip("The road point is this many seconds ahead at the current speed...")]
    [SerializeField] private float roadLookAheadTime = 1f;
    [Tooltip("...kept between these distances (m).")]
    [SerializeField] private Vector2 roadLookAheadRange = new Vector2(8f, 40f);
    [Tooltip("Farther than this from the road (m), the view stops looking for it.")]
    [SerializeField] private float maxRoadDistance = 30f;
    [Tooltip("Below this speed (km/h) the view doesn't look at the road.")]
    [SerializeField] private float roadLookMinSpeedKph = 10f;
    [Tooltip("Angle to the road point (degrees) where following starts to fade out, and where it is fully gone. Keeps spins from swinging the view.")]
    [SerializeField] private Vector2 roadLookFadeDegrees = new Vector2(45f, 90f);
    [Tooltip("Extra look into the turn at full steering (degrees). 0 = off.")]
    [SerializeField] private float steerLook = 0f;
    [Tooltip("How quickly the look direction catches up (s).")]
    [SerializeField] private float lookSmoothTime = 0.25f;

    [Header("Manual Look")]
    [Tooltip("How far the Camera input turns the view left/right at full deflection (degrees).")]
    [SerializeField] private float maxManualLook = 100f;
    [Tooltip("How quickly the view turns to and back from a manual look (s).")]
    [SerializeField] private float manualLookSmoothTime = 0.12f;
    [Tooltip("How much of the automatic look-at-road stays active while looking manually (0 = none).")]
    [Range(0f, 1f)] [SerializeField] private float autoLookWhileManual = 0.3f;

    private Camera cachedCamera;
    private CarController car;
    private CarInputReader carInput;
    private RoadStreamGenerator road;
    private Rigidbody carBody;
    private Vector3 lastVelocity;
    private Vector3 smoothedAcceleration;
    private Vector3 swayOffset;
    private Vector3 swayVelocity;
    private Vector3 slamOffset;
    private Vector3 slamVelocity;
    private float shake;
    private float lookYaw;
    private float lookYawVelocity;
    private float manualYaw;
    private float manualYawVelocity;
    private float noiseSeed;

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

        SetTarget(target);
        road = FindFirstObjectByType<RoadStreamGenerator>();
        noiseSeed = Random.Range(0f, 100f);
    }

    private void OnDestroy()
    {
        if (car != null)
        {
            car.Impact -= OnImpact;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        if (car != null)
        {
            car.Impact -= OnImpact;
        }

        target = newTarget;
        car = target != null ? target.GetComponent<CarController>() : null;
        carInput = target != null ? target.GetComponent<CarInputReader>() : null;
        carBody = target != null ? target.GetComponent<Rigidbody>() : null;
        if (car != null)
        {
            car.Impact += OnImpact;
        }

        lastVelocity = carBody != null ? carBody.linearVelocity : Vector3.zero;
    }

    // Car acceleration in car axes, measured at the physics rate and smoothed.
    private void FixedUpdate()
    {
        if (carBody == null)
        {
            return;
        }

        float deltaTime = Time.fixedDeltaTime;
        Vector3 velocity = carBody.linearVelocity;
        Vector3 acceleration = target.InverseTransformDirection((velocity - lastVelocity) / deltaTime);
        lastVelocity = velocity;

        float maxAcceleration = maxSwayG * Gravity;
        acceleration = Vector3.ClampMagnitude(acceleration, maxAcceleration);
        smoothedAcceleration = Vector3.Lerp(smoothedAcceleration, acceleration, 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.001f, swayAccelerationSmoothing)));
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float deltaTime = Mathf.Min(Time.deltaTime, 0.05f);
        Vector3 swayRest = -Vector3.Scale(swayPerG, smoothedAcceleration / Gravity);
        StepSpring(ref swayOffset, ref swayVelocity, swayRest, swayFrequency, swayDamping, maxSwayOffset, deltaTime);
        StepSpring(ref slamOffset, ref slamVelocity, Vector3.zero, slamFrequency, slamDamping, maxSlamOffset, deltaTime);
        UpdateLook(deltaTime);
        shake = Mathf.MoveTowards(shake, 0f, shakeDecay * deltaTime);

        float speedKph = car != null ? car.SpeedMps * 3.6f : 0f;
        float vibration = Mathf.Clamp01(speedKph / Mathf.Max(1f, vibrationFullSpeedKph)) * (car == null || car.IsGrounded ? 1f : 0.2f);
        float shakeStrength = shake * shake;
        float time = Time.time;

        Vector3 jitter = Noise3(time * roadVibrationFrequency, 0f) * (roadVibrationAmount * vibration)
                         + Noise3(time * shakeFrequency, 50f) * (shakeAmount * shakeStrength);
        Vector3 shakeTilt = Noise3(time * shakeFrequency, 150f) * (shakeRotation * shakeStrength);

        // Only impacts tilt the head: nod from forward/back slam, roll from sideways slam, plus shake.
        float slamNod = slamOffset.z * slamNodPerMeter;
        float slamRoll = -slamOffset.x * slamRollPerMeter;

        Vector3 localEye = eyeOffset + swayOffset + slamOffset + jitter;
        Quaternion look = Quaternion.Euler(0f, lookYaw + manualYaw + shakeTilt.y, 0f)
                          * Quaternion.Euler(pitchDegrees + slamNod + shakeTilt.x, 0f, slamRoll + shakeTilt.z);

        transform.SetPositionAndRotation(target.position + target.rotation * localEye, target.rotation * look);
        cachedCamera.fieldOfView = fieldOfView;
        cachedCamera.nearClipPlane = nearClipPlane;
    }

    private static void StepSpring(ref Vector3 offset, ref Vector3 velocity, Vector3 rest, float frequency, float dampingRatio, Vector3 limit, float deltaTime)
    {
        float omega = 2f * Mathf.PI * Mathf.Max(0.1f, frequency);
        velocity += (omega * omega * (rest - offset) - 2f * dampingRatio * omega * velocity) * deltaTime;
        offset += velocity * deltaTime;
        offset = new Vector3(
            Mathf.Clamp(offset.x, -limit.x, limit.x),
            Mathf.Clamp(offset.y, -limit.y, limit.y),
            Mathf.Clamp(offset.z, -limit.z, limit.z)
        );
    }

    // Turn toward a point on the road ahead, fading out at low speed, off the road and in spins.
    private void UpdateLook(float deltaTime)
    {
        float lookTarget = GetRoadLookYaw();

        float manualTarget = 0f;
        if (carInput != null)
        {
            lookTarget += carInput.Steer * steerLook;
            manualTarget = Mathf.Clamp(carInput.CameraLook, -1f, 1f) * maxManualLook;
        }

        // While looking around by hand, mostly drop the automatic look so it doesn't fight the player.
        float manualAmount = Mathf.Clamp01(Mathf.Abs(manualYaw) / Mathf.Max(1f, maxManualLook * 0.25f));
        lookTarget *= Mathf.Lerp(1f, autoLookWhileManual, manualAmount);

        lookYaw = Mathf.SmoothDamp(lookYaw, lookTarget, ref lookYawVelocity, lookSmoothTime, Mathf.Infinity, deltaTime);
        manualYaw = Mathf.SmoothDamp(manualYaw, manualTarget, ref manualYawVelocity, manualLookSmoothTime, Mathf.Infinity, deltaTime);
    }

    private float GetRoadLookYaw()
    {
        if (car == null || road == null)
        {
            return 0f;
        }

        float speed = car.SpeedMps;
        float speedWeight = Mathf.InverseLerp(roadLookMinSpeedKph, roadLookMinSpeedKph * 2f, speed * 3.6f);
        float lookAhead = Mathf.Clamp(speed * roadLookAheadTime, roadLookAheadRange.x, roadLookAheadRange.y);
        if (speedWeight <= 0f || !road.TryGetRoadPointAhead(target.position, target.forward, lookAhead, maxRoadDistance, out Vector3 roadPoint))
        {
            return 0f;
        }

        Vector3 forward = Vector3.ProjectOnPlane(target.forward, target.up);
        Vector3 toRoad = Vector3.ProjectOnPlane(roadPoint - transform.position, target.up);
        float angle = Vector3.SignedAngle(forward, toRoad, target.up);
        float fadeWeight = 1f - Mathf.InverseLerp(roadLookFadeDegrees.x, roadLookFadeDegrees.y, Mathf.Abs(angle));
        return Mathf.Clamp(angle * roadLookFollow, -maxRoadLook, maxRoadLook) * speedWeight * fadeWeight;
    }

    // Hard hit: throw the head opposite to the push (a crash into something ahead slams it forward) and shake.
    private void OnImpact(float speedChange, Vector3 pushDirection)
    {
        if (target == null)
        {
            return;
        }

        Vector3 localPush = target.InverseTransformDirection(pushDirection);
        Vector3 slam = -localPush * Mathf.Min(speedChange * slamPerImpactSpeed, maxSlamVelocity);
        slamVelocity += slam;
        shake = Mathf.Clamp01(shake + speedChange * shakePerImpactSpeed);
    }

    private Vector3 Noise3(float time, float offset)
    {
        return new Vector3(
            Mathf.PerlinNoise(time, noiseSeed + offset) * 2f - 1f,
            Mathf.PerlinNoise(noiseSeed + offset + 7.3f, time) * 2f - 1f,
            Mathf.PerlinNoise(time + 3.1f, noiseSeed + offset + 13.7f) * 2f - 1f
        );
    }

}
