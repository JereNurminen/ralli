using UnityEngine;

[CreateAssetMenu(menuName = "Ralli/Road/Road Generation Config", fileName = "RoadGenerationConfig")]
public class RoadGenerationConfig : ScriptableObject
{
    [Header("Determinism")]
    [Tooltip("Seed for deterministic road generation.")]
    public int seed = 1337;

    [Header("Chunking")]
    [Tooltip("Road chunk length in meters.")]
    public float chunkLength = 140f;
    [Tooltip("Samples per chunk. Higher = smoother mesh, more vertices.")]
    public int samplesPerChunk = 80;
    [Tooltip("How many chunks to keep generated ahead of player chunk.")]
    public int chunksAhead = 8;
    [Tooltip("How many chunks to keep behind player chunk.")]
    public int chunksBehind = 2;

    [Header("Road Shape")]
    [Tooltip("Asphalt width in meters.")]
    public float roadWidth = 8f;
    [Tooltip("Road mesh thickness in meters.")]
    public float roadThickness = 0.35f;
    [Tooltip("Noise frequency for curvature changes.")]
    public float curvatureFrequency = 0.006f;
    [Tooltip("Max heading change rate in deg/m.")]
    public float maxTurnRateDegPerMeter = 0.22f;
    [Tooltip("How quickly turn rate moves toward noise target (0..1 per sample).")]
    [Range(0.01f, 1f)] public float turnRateResponse = 0.08f;

    [Header("Banking")]
    [Tooltip("How strongly curvature affects banking.")]
    public float bankFromCurvature = 8f;
    [Tooltip("Maximum bank angle in degrees.")]
    public float maxBankAngle = 8f;
    [Tooltip("Minimum turn rate before any banking is applied (deg/m). Straights stay flat.")]
    public float bankTurnRateDeadzone = 0.08f;
    [Tooltip("How quickly bank target follows curvature changes (0..1 per sample).")]
    [Range(0.01f, 1f)] public float bankTargetResponse = 0.08f;
    [Tooltip("How fast bank angle can change (deg/m).")]
    public float bankChangeRateDegPerMeter = 1.0f;

    [Header("Debug")]
    [Tooltip("Draw centerline and frame gizmos.")]
    public bool drawGizmos = true;
    [Tooltip("Draw frame vectors every N samples.")]
    public int gizmoFrameStride = 8;
    [Tooltip("Length of frame gizmos in meters.")]
    public float gizmoFrameLength = 1.5f;
    [Tooltip("Log numeric smoothness diagnostics when road is rebuilt.")]
    public bool logSmoothnessDiagnostics = true;
    [Tooltip("Warn when seam kink angle exceeds this value (degrees).")]
    public float seamKinkWarningDeg = 2.5f;
}
