using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[ExecuteInEditMode]
[RequireComponent(typeof(SplineContainer))]
public class DesignedRoadPieceAuthoring : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Source spline to bake from.")]
    public SplineContainer splineContainer;

    [Tooltip("Road config for preview mesh generation.")]
    public RoadGenerationConfig roadConfig;

    [Tooltip("Target ScriptableObject to write baked data to.")]
    public DesignedRoadPiece targetPiece;

    [Header("Baking Settings")]
    [Tooltip("Sample interval in meters.")]
    [Range(0.1f, 2f)]
    public float sampleInterval = 0.5f;

    [Header("Preview")]
    [Tooltip("Show preview mesh in scene.")]
    public bool showPreview = true;

    [Tooltip("Show turn rate visualization gizmo.")]
    public bool showTurnRateGraph = true;

    [Header("Computed Metadata (Read-Only)")]
    [SerializeField] private float computedArcLength;
    [SerializeField] private float computedTotalYawDeg;
    [SerializeField] private float computedEntryTurnRate;
    [SerializeField] private float computedExitTurnRate;

    public float ComputedArcLength => computedArcLength;
    public float ComputedTotalYawDeg => computedTotalYawDeg;
    public float ComputedEntryTurnRate => computedEntryTurnRate;
    public float ComputedExitTurnRate => computedExitTurnRate;

    private MeshFilter previewMeshFilter;
    private MeshRenderer previewMeshRenderer;

    private void OnEnable()
    {
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();
    }

    public struct BakeResult
    {
        public float arcLength;
        public float totalYawDeltaDeg;
        public float entryTurnRate;
        public float exitTurnRate;
        public AnimationCurve turnRateCurve;
        public AnimationCurve elevationCurve;
        public bool hasElevation;
        public Vector3[] samplePositions;
        public Vector3[] sampleTangents;
        public float[] sampleTurnRates;
    }

    public BakeResult SampleSpline()
    {
        var result = new BakeResult();

        if (splineContainer == null || splineContainer.Spline == null)
            return result;

        var spline = splineContainer.Spline;
        float arcLength = spline.GetLength();
        if (arcLength < 0.01f)
            return result;

        result.arcLength = arcLength;

        int sampleCount = Mathf.Max(3, Mathf.CeilToInt(arcLength / sampleInterval));
        var positions = new Vector3[sampleCount];
        var tangents = new Vector3[sampleCount];
        float[] distances = new float[sampleCount];
        float[] yawAngles = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)(sampleCount - 1);
            distances[i] = t * arcLength;

            spline.Evaluate(t, out float3 localPos, out float3 localTangent, out _);
            positions[i] = transform.TransformPoint((Vector3)localPos);
            tangents[i] = transform.TransformDirection((Vector3)localTangent).normalized;
            yawAngles[i] = Mathf.Atan2(tangents[i].x, tangents[i].z) * Mathf.Rad2Deg;
        }

        // Total yaw delta
        float totalYawDeg = 0f;
        for (int i = 1; i < sampleCount; i++)
            totalYawDeg += Mathf.DeltaAngle(yawAngles[i - 1], yawAngles[i]);

        result.totalYawDeltaDeg = totalYawDeg;

        // Turn rates using central differences
        float[] turnRates = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            int prev = Mathf.Max(0, i - 1);
            int next = Mathf.Min(sampleCount - 1, i + 1);
            float yawDelta = Mathf.DeltaAngle(yawAngles[prev], yawAngles[next]);
            float distDelta = distances[next] - distances[prev];
            turnRates[i] = distDelta > 0.001f ? yawDelta / distDelta : 0f;
        }

        result.entryTurnRate = turnRates[0];
        result.exitTurnRate = turnRates[sampleCount - 1];

        // Build AnimationCurve for turn rate
        var turnRateKeys = new Keyframe[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float normalizedDist = distances[i] / arcLength;
            turnRateKeys[i] = new Keyframe(normalizedDist, turnRates[i]);
        }
        result.turnRateCurve = new AnimationCurve(turnRateKeys);

        // Build AnimationCurve for elevation
        float startY = positions[0].y;
        float maxElevDelta = 0f;
        var elevationKeys = new Keyframe[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float normalizedDist = distances[i] / arcLength;
            float relElev = positions[i].y - startY;
            elevationKeys[i] = new Keyframe(normalizedDist, relElev);
            maxElevDelta = Mathf.Max(maxElevDelta, Mathf.Abs(relElev));
        }
        result.elevationCurve = new AnimationCurve(elevationKeys);
        result.hasElevation = maxElevDelta > 0.5f;

        result.samplePositions = positions;
        result.sampleTangents = tangents;
        result.sampleTurnRates = turnRates;

        return result;
    }

    public void UpdateComputedMetadata()
    {
        var result = SampleSpline();
        computedArcLength = result.arcLength;
        computedTotalYawDeg = result.totalYawDeltaDeg;
        computedEntryTurnRate = result.entryTurnRate;
        computedExitTurnRate = result.exitTurnRate;
    }

    private void OnDrawGizmos()
    {
        if (!showTurnRateGraph)
            return;
        if (splineContainer == null || splineContainer.Spline == null)
            return;

        var result = SampleSpline();
        if (result.samplePositions == null || result.samplePositions.Length < 2)
            return;

        for (int i = 0; i < result.samplePositions.Length; i++)
        {
            Vector3 pos = result.samplePositions[i];
            float turnRate = result.sampleTurnRates[i];
            float graphHeight = Mathf.Abs(turnRate) * 5f;

            Gizmos.color = turnRate > 0f ? Color.yellow : Color.cyan;
            Gizmos.DrawLine(pos + Vector3.up * 0.5f, pos + Vector3.up * (0.5f + graphHeight));
        }
    }
}
