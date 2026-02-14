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
    [Tooltip("Minimum chunk length in meters. If <= 0, chunkLength is used.")]
    public float minChunkLength = 100f;
    [Tooltip("Maximum chunk length in meters. If <= 0, chunkLength is used.")]
    public float maxChunkLength = 180f;
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
    [Tooltip("Shoulder width on each side (dirt band) in meters.")]
    public float shoulderWidth = 1.2f;
    [Tooltip("Shoulder vertical drop from asphalt in meters.")]
    public float shoulderDrop = 0.05f;
    [Tooltip("Ditch width on each side beyond shoulder in meters.")]
    public float ditchWidth = 2.0f;
    [Tooltip("Ditch depth below shoulder level in meters.")]
    public float ditchDepth = 0.55f;
    [Tooltip("Max heading change rate in deg/m.")]
    public float maxTurnRateDegPerMeter = 0.22f;
    [Tooltip("How quickly turn rate moves toward piece target (0..1 per sample).")]
    [Range(0.01f, 1f)] public float turnRateResponse = 0.08f;

    [Header("Elevation")]
    [Tooltip("Enable procedural hills and bumps.")]
    public bool enableHills = true;
    [Tooltip("Short bump amplitude in meters.")]
    public float smallBumpAmplitude = 0.9f;
    [Tooltip("Short bump wavelength in meters.")]
    public float smallBumpWavelength = 42f;
    [Tooltip("How often short bump patches appear along the road (0..1).")]
    [Range(0f, 1f)] public float smallBumpOccurrence = 0.28f;
    [Tooltip("Typical length of bump/no-bump patches in meters.")]
    public float smallBumpPatchLength = 140f;
    [Tooltip("Long hill amplitude in meters.")]
    public float largeHillAmplitude = 8f;
    [Tooltip("Long hill wavelength in meters.")]
    public float largeHillWavelength = 360f;
    [Tooltip("Maximum road grade angle in degrees.")]
    public float maxSlopeAngleDeg = 8f;
    [Tooltip("How quickly slope follows target elevation change (0..1 per sample).")]
    [Range(0.01f, 1f)] public float slopeResponse = 0.12f;

    [Header("Road Pieces")]
    [Tooltip("Chance that the next piece is a curve instead of a straight.")]
    [Range(0f, 1f)] public float curvePieceProbability = 0.72f;
    [Tooltip("Minimum straight piece length in meters.")]
    public float minStraightLength = 28f;
    [Tooltip("Maximum straight piece length in meters.")]
    public float maxStraightLength = 90f;
    [Tooltip("Minimum curve piece length in meters.")]
    public float minCurveLength = 30f;
    [Tooltip("Maximum curve piece length in meters.")]
    public float maxCurveLength = 75f;
    [Tooltip("Minimum absolute curve turn rate in deg/m.")]
    public float minCurveTurnRateDegPerMeter = 0.12f;
    [Tooltip("Maximum absolute curve turn rate in deg/m.")]
    public float maxCurveTurnRateDegPerMeter = 0.30f;
    [Tooltip("Chance to flip curve direction vs previous curve.")]
    [Range(0f, 1f)] public float oppositeCurveChance = 0.65f;

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
    [Tooltip("Draw gizmo markers at chunk seam boundaries.")]
    public bool drawSeamMarkers = true;
    [Tooltip("Seam marker sphere radius in meters.")]
    public float seamMarkerRadius = 0.6f;
    [Tooltip("Seam marker vertical line height in meters.")]
    public float seamMarkerHeight = 2.2f;
}
