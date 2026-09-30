using UnityEngine;

// Driver's-eye camera. The eye sits at a fixed point in the car; on top of that:
// - Head sway: a spring-damped "head" pushed around by the car's acceleration (corners, braking,
//   bumps, landings), which also tilts and nods the view.
// - Road vibration that grows with speed.
// - A violent head slam plus shake on hard impacts.
// - Looking into the slide: the view turns toward where the car is travelling, fading out when
//   the car spins so it never swings wildly.
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

    [Header("Head Sway")]
    [Tooltip("How far the head moves per g of car acceleration (m): x = sideways, y = up/down, z = forward/back.")]
    [SerializeField] private Vector3 headSwayPerG = new Vector3(0.035f, 0.025f, 0.04f);
    [Tooltip("Head travel limit per axis (m).")]
    [SerializeField] private Vector3 maxHeadOffset = new Vector3(0.08f, 0.06f, 0.14f);
    [Tooltip("Head roll (degrees) per g of sideways sway. Leans away from the turn.")]
    [SerializeField] private float headRollPerG = 4f;
    [Tooltip("Head nod (degrees) per g of forward/back sway. Nods down under braking and impacts.")]
    [SerializeField] private float headPitchPerG = 3f;
    [Tooltip("Head spring frequency (Hz). Lower = floppier.")]
    [SerializeField] private float headSpringFrequency = 2.2f;
    [Tooltip("Head spring damping. 1 = no bounce, lower = wobblier.")]
    [Range(0.05f, 1.5f)] [SerializeField] private float headDamping = 0.55f;
    [Tooltip("Smoothing of the car acceleration fed to the head (s). Filters physics jitter.")]
    [SerializeField] private float accelerationSmoothing = 0.05f;
    [Tooltip("Acceleration above this (g) is ignored by the sway; hard hits go through the impact slam instead.")]
    [SerializeField] private float maxSwayG = 2.5f;

    [Header("Road Vibration")]
    [Tooltip("Position jitter at full vibration speed (m).")]
    [SerializeField] private float vibrationAmount = 0.003f;
    [Tooltip("Rotation jitter at full vibration speed (degrees).")]
    [SerializeField] private float vibrationRotation = 0.3f;
    [Tooltip("Jitter speed (Hz-ish).")]
    [SerializeField] private float vibrationFrequency = 18f;
    [Tooltip("Speed (km/h) where vibration reaches full strength.")]
    [SerializeField] private float vibrationFullSpeedKph = 160f;

    [Header("Impact Slam")]
    [Tooltip("Head velocity (m/s) thrown opposite the push, per 1 m/s of impact speed change.")]
    [SerializeField] private float slamPerImpactSpeed = 0.35f;
    [Tooltip("Cap on the slam head velocity (m/s).")]
    [SerializeField] private float maxSlamVelocity = 4f;
    [Tooltip("Shake added per 1 m/s of impact speed change (shake is 0..1).")]
    [SerializeField] private float shakePerImpactSpeed = 0.08f;
    [Tooltip("How fast shake dies out (per second).")]
    [SerializeField] private float shakeDecay = 1.6f;
    [Tooltip("Position shake at full shake (m).")]
    [SerializeField] private float shakeAmount = 0.04f;
    [Tooltip("Rotation shake at full shake (degrees).")]
    [SerializeField] private float shakeRotation = 5f;
    [SerializeField] private float shakeFrequency = 22f;

    [Header("Look Into Slide")]
    [Tooltip("Share of the drift angle the view turns toward (0 = off, 1 = look straight along travel).")]
    [Range(0f, 1f)] [SerializeField] private float slideFollow = 0.5f;
    [Tooltip("Max turn toward the slide (degrees).")]
    [SerializeField] private float maxSlideLook = 25f;
    [Tooltip("Below this speed (km/h) the view doesn't follow the slide.")]
    [SerializeField] private float slideLookMinSpeedKph = 15f;
    [Tooltip("Drift angle (degrees) where following starts to fade out, and where it is fully gone. Keeps spins from swinging the view.")]
    [SerializeField] private Vector2 spinFadeDegrees = new Vector2(35f, 75f);
    [Tooltip("Extra look into the turn at full steering (degrees).")]
    [SerializeField] private float steerLook = 3f;
    [Tooltip("How quickly the look direction catches up (s).")]
    [SerializeField] private float lookSmoothTime = 0.25f;

    [Header("Manual Look")]
    [Tooltip("How far the Camera input turns the view left/right at full deflection (degrees).")]
    [SerializeField] private float maxManualLook = 100f;
    [Tooltip("How quickly the view turns to and back from a manual look (s).")]
    [SerializeField] private float manualLookSmoothTime = 0.12f;
    [Tooltip("How much of the automatic look-into-slide stays active while looking manually (0 = none).")]
    [Range(0f, 1f)] [SerializeField] private float autoLookWhileManual = 0.3f;

    private Camera cachedCamera;
    private CarController car;
    private CarInputReader carInput;
    private Rigidbody carBody;
    private Vector3 lastVelocity;
    private Vector3 smoothedAcceleration;
    private Vector3 headOffset;
    private Vector3 headVelocity;
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
        smoothedAcceleration = Vector3.Lerp(smoothedAcceleration, acceleration, 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.001f, accelerationSmoothing)));
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float deltaTime = Mathf.Min(Time.deltaTime, 0.05f);
        UpdateHead(deltaTime);
        UpdateLook(deltaTime);
        shake = Mathf.MoveTowards(shake, 0f, shakeDecay * deltaTime);

        float speedKph = car != null ? car.SpeedMps * 3.6f : 0f;
        float vibration = Mathf.Clamp01(speedKph / Mathf.Max(1f, vibrationFullSpeedKph)) * (car == null || car.IsGrounded ? 1f : 0.2f);
        float shakeStrength = shake * shake;
        float time = Time.time;

        Vector3 jitter = Noise3(time * vibrationFrequency, 0f) * (vibrationAmount * vibration)
                         + Noise3(time * shakeFrequency, 50f) * (shakeAmount * shakeStrength);
        Vector3 jitterRotation = Noise3(time * vibrationFrequency, 100f) * (vibrationRotation * vibration)
                                 + Noise3(time * shakeFrequency, 150f) * (shakeRotation * shakeStrength);

        float headRoll = -SafeDivide(headOffset.x, headSwayPerG.x) * headRollPerG;
        float headPitch = SafeDivide(headOffset.z, headSwayPerG.z) * headPitchPerG;

        Vector3 localEye = eyeOffset + headOffset + jitter;
        Quaternion look = Quaternion.Euler(0f, lookYaw + manualYaw + jitterRotation.y, 0f)
                          * Quaternion.Euler(pitchDegrees + headPitch + jitterRotation.x, 0f, headRoll + jitterRotation.z);

        transform.SetPositionAndRotation(target.position + target.rotation * localEye, target.rotation * look);
        cachedCamera.fieldOfView = fieldOfView;
        cachedCamera.nearClipPlane = nearClipPlane;
    }

    // Spring-damped head: acceleration shoves it the opposite way, the spring pulls it back.
    private void UpdateHead(float deltaTime)
    {
        float omega = 2f * Mathf.PI * Mathf.Max(0.1f, headSpringFrequency);
        float stiffness = omega * omega;
        float damping = 2f * headDamping * omega;
        Vector3 rest = -Vector3.Scale(headSwayPerG, smoothedAcceleration / Gravity);

        headVelocity += (stiffness * (rest - headOffset) - damping * headVelocity) * deltaTime;
        headOffset += headVelocity * deltaTime;
        headOffset = new Vector3(
            Mathf.Clamp(headOffset.x, -maxHeadOffset.x, maxHeadOffset.x),
            Mathf.Clamp(headOffset.y, -maxHeadOffset.y, maxHeadOffset.y),
            Mathf.Clamp(headOffset.z, -maxHeadOffset.z, maxHeadOffset.z)
        );
    }

    // Turn toward the direction of travel, fading out at low speed and in spins.
    private void UpdateLook(float deltaTime)
    {
        float lookTarget = 0f;
        if (car != null)
        {
            float drift = car.DriftAngle;
            float speedWeight = Mathf.InverseLerp(slideLookMinSpeedKph, slideLookMinSpeedKph * 2f, car.SpeedMps * 3.6f);
            float spinWeight = 1f - Mathf.InverseLerp(spinFadeDegrees.x, spinFadeDegrees.y, Mathf.Abs(drift));
            lookTarget = Mathf.Clamp(drift * slideFollow, -maxSlideLook, maxSlideLook) * speedWeight * spinWeight;
        }

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

    // Hard hit: throw the head opposite to the push (a crash into something ahead slams it forward) and shake.
    private void OnImpact(float speedChange, Vector3 pushDirection)
    {
        if (target == null)
        {
            return;
        }

        Vector3 localPush = target.InverseTransformDirection(pushDirection);
        Vector3 slam = -localPush * Mathf.Min(speedChange * slamPerImpactSpeed, maxSlamVelocity);
        headVelocity += slam;
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

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / divisor : 0f;
    }
}
