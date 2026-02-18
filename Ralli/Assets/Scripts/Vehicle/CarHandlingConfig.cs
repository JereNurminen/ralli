using UnityEngine;

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

    [Header("Suspension")]
    [Tooltip("Wheel visual/physical radius in meters.")]
    public float wheelRadius = 0.35f;
    [Tooltip("Suspension travel from fully extended to compressed (meters).")]
    public float suspensionRestLength = 0.5f;
    [Tooltip("Suspension spring stiffness. Higher = stiffer body support.")]
    public float springStrength = 32000f;
    [Tooltip("Suspension damping. Higher = less bounce, slower weight transfer.")]
    public float damperStrength = 3800f;
    [Tooltip("Clamp for suspension compression speed used by damper (m/s).")]
    public float maxSuspensionVelocity = 7f;
    [Tooltip("Maximum suspension force as multiplier of static per-wheel load.")]
    public float maxSuspensionLoadFactor = 2.7f;
    [Tooltip("Force resisting body roll between left/right wheels on each axle.")]
    public float antiRollStiffness = 9000f;

    [Header("Grip")]
    [Tooltip("Overall tire force budget from wheel load. Higher = more total grip.")]
    public float tireFriction = 1.15f;
    [Tooltip("Front axle sideways grip response. Higher = sharper turn-in.")]
    public float frontLateralGrip = 10.5f;
    [Tooltip("Rear axle sideways grip response. Lower = easier rotation/drift.")]
    public float rearLateralGrip = 7.0f;
    [Tooltip("Rear grip multiplier while handbrake is held.")]
    [Range(0.1f, 1f)] public float rearGripWhileHandbrake = 0.35f;
    [Tooltip("Rear lateral grip multiplier when exactly one rear wheel is grounded.")]
    [Range(0f, 1f)] public float rearGripWhenSingleRearWheelGrounded = 0.4f;
    [Tooltip("Rear tire force budget multiplier when exactly one rear wheel is grounded.")]
    [Range(0f, 1f)] public float rearTireForceCapWhenSingleRearWheelGrounded = 0.4f;

    [Header("Power + Brakes")]
    [Tooltip("Drive torque split to rear axle. 1 = RWD, 0.5 = even AWD.")]
    [Range(0f, 1f)] public float rearDriveBias = 1f;
    [Tooltip("Maximum engine drive force.")]
    public float maxDriveForce = 7000f;
    [Tooltip("Service brake force when brake input is pressed.")]
    public float maxBrakeForce = 14000f;
    [Tooltip("Total additional brake force applied by handbrake.")]
    public float handbrakeForce = 17000f;
    [Tooltip("Handbrake force split to rear axle. 1 = rear-only, 0.5 = even front/rear.")]
    [Range(0f, 1f)] public float handbrakeRearBias = 1f;
    [Tooltip("Rear drive force multiplier when exactly one rear wheel is grounded.")]
    [Range(0f, 1f)] public float rearDriveWhenSingleRearWheelGrounded = 0.35f;
    [Tooltip("Rear suspension force multiplier when exactly one rear wheel is grounded.")]
    [Range(0f, 1f)] public float rearSpringForceWhenSingleRearWheelGrounded = 0.6f;
    [Tooltip("Top speed (km/h) used to normalize fake-gear banding.")]
    public float fakeGearTopSpeedKph = 198f;
    [Tooltip("Speed (km/h) -> normalized drive force multiplier.")]
    public AnimationCurve baseDriveForceBySpeedKph = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(72f, 1f),
        new Keyframe(126f, 0.82f),
        new Keyframe(198f, 0.58f),
        new Keyframe(270f, 0.35f)
    );
    [Tooltip("Number of fake gears used for drive force modulation.")]
    [Range(1, 8)] public int fakeGearCount = 5;
    [Tooltip("Optional normalized thresholds (0..1) between gear bands. Leave empty for even spacing.")]
    public float[] fakeGearThresholds01;
    [Tooltip("Torque reduction fraction during fake shifts.")]
    [Range(0f, 1f)] public float shiftTorqueDip = 0.75f;
    [Tooltip("Fake shift pause duration in seconds.")]
    [Range(0.01f, 0.2f)] public float shiftPauseDuration = 0.08f;
    [Tooltip("Engine braking strength as fraction of max drive force.")]
    [Range(0f, 1f)] public float engineBrakingStrength = 0.2f;

    [Header("Coasting")]
    [Tooltip("Base rolling slowdown when no throttle/brake is applied.")]
    public float rollingResistance = 180f;
    [Tooltip("Speed-squared drag. Higher = stronger high-speed deceleration.")]
    public float aerodynamicDrag = 1.35f;

    [Header("Tire Slip Curves")]
    [Tooltip("Lateral slip speed (normalized via lateralSlipScale) -> grip multiplier (0..1). Peak near x=1.0.")]
    public AnimationCurve lateralSlipCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 0.85f),
        new Keyframe(1.0f, 1.0f),
        new Keyframe(2.0f, 0.78f),
        new Keyframe(4.0f, 0.55f)
    );
    [Tooltip("Scales lateral speed (m/s) before curve lookup. Higher = peak grip reached at lower slip speeds.")]
    public float lateralSlipScale = 4.0f;
    [Tooltip("Longitudinal slip ratio (0..1+) -> traction multiplier (0..1). Peak near 0.15.")]
    public AnimationCurve longitudinalSlipCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.15f, 1.0f),
        new Keyframe(0.35f, 0.82f),
        new Keyframe(1.0f, 0.55f),
        new Keyframe(2.0f, 0.40f)
    );
    [Tooltip("Scales slip ratio before curve lookup.")]
    public float longitudinalSlipScale = 1.0f;

    [Header("Boost")]
    [Tooltip("Drive force multiplier applied to rear wheels while boost is active.")]
    public float boostForceMultiplier = 4.0f;
    [Tooltip("How quickly boost force ramps up (units/second toward 1.0).")]
    public float boostRampUpSpeed = 8f;
    [Tooltip("How quickly boost force ramps down when released.")]
    public float boostRampDownSpeed = 6f;

    [Header("Stability")]
    [Tooltip("Rigidbody center-of-mass Y offset. Lower = more stable, less roll.")]
    public float centerOfMassYOffset = -0.6f;
    [Tooltip("General yaw damping while grounded. Higher = less spin tendency.")]
    public float yawDamping = 2.2f;
    [Tooltip("Roll damping while grounded. Higher = less rocking/roll oscillation.")]
    public float rollDamping = 1.4f;
    [Tooltip("Steer input threshold under which straightening assist can activate.")]
    public float straighteningSteerThreshold = 0.03f;
    [Tooltip("Seconds of near-zero steer before straightening assist fires. Allows Scandinavian flick transitions.")]
    [Range(0f, 0.5f)] public float straighteningActivationDelay = 0.12f;
    [Tooltip("How strongly no-input assist removes sideways velocity.")]
    public float straighteningLateralDamping = 3.5f;
    [Tooltip("How strongly no-input assist damps yaw rotation.")]
    public float straighteningYawDamping = 4.5f;
    [Tooltip("Steering authority kept when both front wheels are airborne.")]
    [Range(0f, 1f)] public float steerWhenFrontAirborne = 0.15f;
    [Tooltip("Rear drive force multiplier when both front wheels are airborne.")]
    [Range(0f, 1f)] public float rearDriveWhenFrontAirborne = 0.5f;
    [Tooltip("Rear lateral grip multiplier when both front wheels are airborne.")]
    [Range(0f, 1f)] public float rearGripWhenFrontAirborne = 0.7f;
}
