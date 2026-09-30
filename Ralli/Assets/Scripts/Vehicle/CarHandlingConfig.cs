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
    [Tooltip("Drag reference speed (km/h). Normal-throttle top speed lands ~5% below this.")]
    public float maxSpeedKph = 210f;
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
    [Tooltip("Deceleration while the handbrake is held (m/s²), on top of the rear grip loss.")]
    public float handbrakeDeceleration = 6f;
    [Tooltip("Acceleration in reverse (m/s²).")]
    public float reverseAcceleration = 3f;
    [Tooltip("Reverse speed cap (km/h).")]
    public float reverseMaxSpeedKph = 25f;
    [Tooltip("Fraction of uphill gravity pull kept. 0 = hills never slow the car, 1 = full gravity. Downhill is unaffected.")]
    [Range(0f, 1f)] public float uphillGravityScale = 0.3f;

    [Header("Overdrive Exit Boost")]
    [Tooltip("Overdrive must be held at least this long (s) for release to give an exit boost.")]
    public float exitBoostMinHold = 0.4f;
    [Tooltip("Instant forward speed kick on release (km/h).")]
    public float exitBoostKickKph = 10f;
    [Tooltip("How long the follow-up push and grip bonus last (s). Fades out linearly.")]
    public float exitBoostDuration = 0.8f;
    [Tooltip("Extra forward acceleration at the start of the exit boost (m/s²).")]
    public float exitBoostAcceleration = 6f;
    [Tooltip("Extra grip on both axles at the start of the exit boost (0.4 = +40%).")]
    public float exitBoostGrip = 0.4f;

    [Header("Fake Engine (RPM for sound and rev counter)")]
    [Tooltip("Speed range covered by each fake gear (km/h).")]
    public float gearSpanKph = 45f;
    public int gearCount = 5;
    [Tooltip("Idle RPM on a 0 (off) to 1 (limiter) scale.")]
    [Range(0f, 1f)] public float idleRpm01 = 0.12f;
    [Tooltip("Extra RPM while on throttle.")]
    [Range(0f, 0.3f)] public float throttleRpmBump = 0.08f;
    [Tooltip("Extra RPM with Overdrive fully in (the engine sounds strained).")]
    [Range(0f, 0.3f)] public float overdriveRpmBump = 0.1f;
    [Tooltip("How fast RPM climbs on the ground (0..1 per second).")]
    public float rpmRiseRate = 3f;
    [Tooltip("How fast RPM falls (0..1 per second). Gear shifts and landings drop at this rate.")]
    public float rpmFallRate = 2f;
    [Tooltip("How fast RPM climbs to the limiter while airborne on throttle (wheels spin free).")]
    public float airborneRpmRiseRate = 5f;

    [Header("Engine Heat")]
    [Tooltip("Heat gained per second while Overdrive is held (0..1). 0.125 = 8 s from cold to max.")]
    public float heatRiseRate = 0.125f;
    [Tooltip("Heat lost per second while Overdrive is released.")]
    public float heatFallRate = 0.1f;
    [Tooltip("Heat level where power starts to taper.")]
    [Range(0f, 1f)] public float heatTaperStart = 0.8f;
    [Tooltip("Power multiplier at full heat.")]
    [Range(0f, 1f)] public float powerAtMaxHeat = 0.6f;

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

    [Header("Drift Assist")]
    [Tooltip("How Overdrive's rear-grip loss follows steering past the threshold: 1 = linear (easy small drifts), 2 = squared (needs big steering).")]
    [Range(0.5f, 3f)] public float overdriveSteerExponent = 1f;
    [Tooltip("Drift angle (degrees) where the spin catcher starts to hold the slide.")]
    public float driftAngleLimit = 35f;
    [Tooltip("Degrees past the limit over which the catcher reaches full strength.")]
    public float spinCatchRange = 20f;
    [Tooltip("How hard rotation that would deepen the slide is damped once past the limit (1/s).")]
    public float spinYawDamping = 6f;
    [Tooltip("Forward push that keeps speed through a slide (m/s², along the direction of travel). 0 = off.")]
    public float driftMomentum = 4f;
    [Tooltip("Drift angle (degrees) where the momentum push starts, and where it is full.")]
    public Vector2 driftMomentumAngles = new Vector2(8f, 20f);

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
