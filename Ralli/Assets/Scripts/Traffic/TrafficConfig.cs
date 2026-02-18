using UnityEngine;

[CreateAssetMenu(menuName = "Ralli/Traffic/Traffic Config", fileName = "TrafficConfig")]
public class TrafficConfig : ScriptableObject
{
    [Header("Spawning")]
    [Tooltip("Enable traffic spawning.")]
    public bool enableTraffic = true;
    [Tooltip("Approximate vehicles per kilometer, split across both lanes.")]
    public float vehiclesPerKilometer = 10f;
    [Tooltip("Density multiplier at max progress.")]
    public float maxDensityMultiplier = 1.8f;
    [Tooltip("Player progress distance (m) where max density multiplier is reached.")]
    public float densityRampDistance = 6000f;
    [Tooltip("Maximum vehicles spawned per chunk.")]
    public int maxVehiclesPerChunk = 4;
    [Tooltip("Minimum longitudinal spacing between traffic vehicles in same lane (m).")]
    public float sameLaneMinSpacing = 24f;
    [Tooltip("Extra chunks ahead of active range where traffic can spawn.")]
    public int spawnAheadChunks = 1;

    [Header("Speed")]
    [Tooltip("Base traffic speed in km/h.")]
    public float trafficSpeedKph = 80f;
    [Tooltip("Random speed variation (+/- km/h).")]
    public float speedVarianceKph = 6f;
    [Tooltip("How quickly traffic reaches target speed (m/s²).")]
    public float accelerationMps2 = 3.0f;
    [Tooltip("How quickly traffic slows down when braking (m/s²).")]
    public float brakingMps2 = 6.0f;

    [Header("Cornering")]
    [Tooltip("Enable corner-based speed reduction.")]
    public bool slowInCorners = true;
    [Tooltip("Turn-rate threshold (deg/m) where slowdown starts.")]
    public float cornerSlowStartTurnRateDegPerMeter = 0.18f;
    [Tooltip("Turn-rate threshold (deg/m) for maximum slowdown.")]
    public float cornerSlowMaxTurnRateDegPerMeter = 0.8f;
    [Tooltip("Speed multiplier at maximum corner slowdown.")]
    [Range(0.2f, 1f)] public float cornerMinSpeedFactor = 0.55f;

    [Header("Lane Placement")]
    [Tooltip("Inset from each asphalt edge used when computing lane centers (m).")]
    public float laneShoulderInset = 0.45f;
    [Tooltip("Chance [0..1] that a spawned vehicle is in player's direction lane.")]
    [Range(0f, 1f)] public float sameDirectionLaneChance = 0.35f;

    [Header("Collision Response")]
    [Tooltip("Distance to player (m) where a traffic car is released from spline-following and becomes dynamic.")]
    [Min(0.05f)] public float ragdollReleaseDistance = 0.5f;

    [Header("Visuals")]
    [Tooltip("Simple traffic body size (x=width, y=height, z=length).")]
    public Vector3 vehicleBoxSize = new Vector3(1.78f, 1.46f, 4.05f);
    [Tooltip("Traffic vehicle rigidbody mass in kilograms.")]
    public float vehicleMassKg = 1100f;
}
