using System.Collections.Generic;
using UnityEngine;

public class RoadStreamGenerator : MonoBehaviour
{
    private class RoadSample
    {
        public float s;
        public Vector3 position;
        public Vector3 tangent;
        public Vector3 right;
        public Vector3 up;
        public float bankAngle;
        public float bankTargetAngle;
        public float turnRateDegPerMeter;
    }

    private class ChunkData
    {
        public int chunkIndex;
        public int sampleStartIndex;
        public int sampleEndIndex;
        public GameObject gameObject;
    }

    [Header("References")]
    [SerializeField] private RoadGenerationConfig config;
    [SerializeField] private Transform target;
    [SerializeField] private Material roadMaterial;

    [Header("Runtime")]
    [SerializeField] private bool generateOnStart = true;

    private readonly List<RoadSample> samples = new List<RoadSample>(4096);
    private readonly Dictionary<int, ChunkData> chunks = new Dictionary<int, ChunkData>();

    private float sampleDistance;

    private void Start()
    {
        if (target == null)
        {
            CarController car = FindFirstObjectByType<CarController>();
            if (car != null)
            {
                target = car.transform;
            }
        }

        if (generateOnStart)
        {
            RebuildFromScratch();
        }
    }

    private void Update()
    {
        if (config == null)
        {
            return;
        }

        EnsureInitialized();

        if (sampleDistance <= 0f)
        {
            sampleDistance = config.chunkLength / Mathf.Max(2, config.samplesPerChunk);
        }

        float playerS = EstimatePlayerS();
        int playerChunk = Mathf.FloorToInt(playerS / Mathf.Max(1f, config.chunkLength));

        int minChunk = playerChunk - Mathf.Max(0, config.chunksBehind);
        int maxChunk = playerChunk + Mathf.Max(1, config.chunksAhead);

        EnsureChunkRange(minChunk, maxChunk);
        CullChunksOutside(minChunk, maxChunk);
    }

    [ContextMenu("Rebuild Road")]
    public void RebuildFromScratch()
    {
        ClearChunks();
        samples.Clear();

        if (config == null)
        {
            return;
        }

        EnsureInitialized();

        EnsureChunkRange(0, Mathf.Max(1, config.chunksAhead));
        LogSmoothnessDiagnostics();
    }

    private void EnsureChunkRange(int minChunk, int maxChunk)
    {
        for (int chunkIndex = minChunk; chunkIndex <= maxChunk; chunkIndex++)
        {
            if (chunkIndex < 0 || chunks.ContainsKey(chunkIndex))
            {
                continue;
            }

            CreateChunk(chunkIndex);
        }
    }

    private void CreateChunk(int chunkIndex)
    {
        int baseSamplesPerChunk = Mathf.Max(2, config.samplesPerChunk);
        int startSampleIndex = chunkIndex * baseSamplesPerChunk;
        int endSampleIndex = (chunkIndex + 1) * baseSamplesPerChunk;

        EnsureSamplesUpToIndex(endSampleIndex);

        GameObject chunkObject = new GameObject($"RoadChunk_{chunkIndex:0000}");
        chunkObject.transform.SetParent(transform, true);
        chunkObject.isStatic = false;

        MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = chunkObject.AddComponent<MeshCollider>();

        if (roadMaterial != null)
        {
            meshRenderer.sharedMaterial = roadMaterial;
        }

        Mesh visualMesh = BuildChunkVisualMesh(chunkIndex, startSampleIndex, endSampleIndex);
        Mesh colliderMesh = BuildChunkColliderMesh(chunkIndex, startSampleIndex, endSampleIndex);
        meshFilter.sharedMesh = visualMesh;
        meshCollider.sharedMesh = colliderMesh;

        ChunkData chunk = new ChunkData
        {
            chunkIndex = chunkIndex,
            sampleStartIndex = startSampleIndex,
            sampleEndIndex = endSampleIndex,
            gameObject = chunkObject
        };

        chunks[chunkIndex] = chunk;
    }

    private Mesh BuildChunkVisualMesh(int chunkIndex, int startSampleIndex, int endSampleIndex)
    {
        // Seam-safe chunk edges: each chunk includes both start and end samples.
        int usableStart = startSampleIndex;
        int usableCount = (endSampleIndex - usableStart) + 1;
        usableCount = Mathf.Max(2, usableCount);

        // Per sample: LT, RT, LB, RB
        Vector3[] vertices = new Vector3[usableCount * 4];
        Color[] colors = new Color[usableCount * 4];
        Vector2[] uv = new Vector2[usableCount * 4];
        int[] triangles = new int[(usableCount - 1) * 24];

        float halfWidth = config.roadWidth * 0.5f;
        float thickness = Mathf.Max(0.05f, config.roadThickness);

        for (int i = 0; i < usableCount; i++)
        {
            RoadSample sample = samples[usableStart + i];

            Vector3 leftTop = sample.position - sample.right * halfWidth;
            Vector3 rightTop = sample.position + sample.right * halfWidth;
            Vector3 down = -sample.up * thickness;
            Vector3 leftBottom = leftTop + down;
            Vector3 rightBottom = rightTop + down;

            int v = i * 4;
            vertices[v] = leftTop;
            vertices[v + 1] = rightTop;
            vertices[v + 2] = leftBottom;
            vertices[v + 3] = rightBottom;

            float vCoord = sample.s * 0.1f;
            uv[v] = new Vector2(0f, vCoord);       // LT
            uv[v + 1] = new Vector2(1f, vCoord);   // RT
            uv[v + 2] = new Vector2(0f, vCoord);   // LB
            uv[v + 3] = new Vector2(1f, vCoord);   // RB

            // Surface pipeline placeholder: pure asphalt for now.
            Color asphalt = new Color(1f, 0f, 0f, 0f);
            colors[v] = asphalt;
            colors[v + 1] = asphalt;
            colors[v + 2] = asphalt;
            colors[v + 3] = asphalt;
        }

        int t = 0;
        for (int i = 0; i < usableCount - 1; i++)
        {
            int v = i * 4;
            int n = v + 4;

            // Top face (LT, RT, next LT, next RT)
            triangles[t++] = v;
            triangles[t++] = n;
            triangles[t++] = v + 1;
            triangles[t++] = v + 1;
            triangles[t++] = n;
            triangles[t++] = n + 1;

            // Bottom face (reverse winding)
            triangles[t++] = v + 2;
            triangles[t++] = v + 3;
            triangles[t++] = n + 2;
            triangles[t++] = v + 3;
            triangles[t++] = n + 3;
            triangles[t++] = n + 2;

            // Left side
            triangles[t++] = v;
            triangles[t++] = v + 2;
            triangles[t++] = n;
            triangles[t++] = v + 2;
            triangles[t++] = n + 2;
            triangles[t++] = n;

            // Right side
            triangles[t++] = v + 1;
            triangles[t++] = n + 1;
            triangles[t++] = v + 3;
            triangles[t++] = v + 3;
            triangles[t++] = n + 1;
            triangles[t++] = n + 3;
        }

        Mesh mesh = new Mesh
        {
            name = $"RoadChunkMesh_{chunkIndex:0000}",
            vertices = vertices,
            colors = colors,
            uv = uv,
            triangles = triangles
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh BuildChunkColliderMesh(int chunkIndex, int startSampleIndex, int endSampleIndex)
    {
        int usableStart = startSampleIndex;
        int usableCount = (endSampleIndex - usableStart) + 1;
        usableCount = Mathf.Max(2, usableCount);

        Vector3[] vertices = new Vector3[usableCount * 2];
        int[] triangles = new int[(usableCount - 1) * 6];

        float halfWidth = config.roadWidth * 0.5f;

        for (int i = 0; i < usableCount; i++)
        {
            RoadSample sample = samples[usableStart + i];
            int v = i * 2;
            vertices[v] = sample.position - sample.right * halfWidth;
            vertices[v + 1] = sample.position + sample.right * halfWidth;
        }

        int t = 0;
        for (int i = 0; i < usableCount - 1; i++)
        {
            int v = i * 2;
            triangles[t++] = v;
            triangles[t++] = v + 2;
            triangles[t++] = v + 1;
            triangles[t++] = v + 1;
            triangles[t++] = v + 2;
            triangles[t++] = v + 3;
        }

        Mesh mesh = new Mesh
        {
            name = $"RoadChunkCollider_{chunkIndex:0000}",
            vertices = vertices,
            triangles = triangles
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void EnsureSamplesUpToIndex(int targetIndex)
    {
        EnsureInitialized();

        for (int i = samples.Count; i <= targetIndex; i++)
        {
            RoadSample prev = samples[i - 1];
            float s = i * sampleDistance;

            float rawTurnRateDegPerMeter = GetTurnRateDegPerMeter(s);
            float turnRateDegPerMeter = Mathf.Lerp(
                prev.turnRateDegPerMeter,
                rawTurnRateDegPerMeter,
                Mathf.Clamp01(config.turnRateResponse)
            );
            float yawDelta = turnRateDegPerMeter * sampleDistance;
            float prevYaw = Mathf.Atan2(prev.tangent.x, prev.tangent.z) * Mathf.Rad2Deg;
            float yaw = prevYaw + yawDelta;
            Vector3 tangent = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            float absTurnRate = Mathf.Abs(turnRateDegPerMeter);
            float rawBankTarget = 0f;
            if (absTurnRate > config.bankTurnRateDeadzone && config.maxTurnRateDegPerMeter > 0.001f)
            {
                float denom = Mathf.Max(0.001f, config.maxTurnRateDegPerMeter - config.bankTurnRateDeadzone);
                float tCurve = Mathf.Clamp01((absTurnRate - config.bankTurnRateDeadzone) / denom);
                tCurve = tCurve * tCurve * (3f - 2f * tCurve);
                float signed = Mathf.Sign(turnRateDegPerMeter);
                rawBankTarget = signed * Mathf.Min(config.maxBankAngle, config.bankFromCurvature) * tCurve;
            }

            float targetBankAngle = Mathf.Lerp(
                prev.bankTargetAngle,
                rawBankTarget,
                Mathf.Clamp01(config.bankTargetResponse)
            );
            float maxBankStep = Mathf.Max(0.01f, config.bankChangeRateDegPerMeter) * sampleDistance;
            float bankAngle = Mathf.MoveTowards(prev.bankAngle, targetBankAngle, maxBankStep);

            Vector3 rightFlat = Vector3.Cross(Vector3.up, tangent).normalized;
            if (rightFlat.sqrMagnitude < 0.0001f)
            {
                rightFlat = prev.right;
            }

            Quaternion bankRotation = Quaternion.AngleAxis(bankAngle, tangent);
            Vector3 up = (bankRotation * Vector3.up).normalized;
            Vector3 right = (bankRotation * rightFlat).normalized;

            Vector3 position = prev.position + tangent * sampleDistance;

            RoadSample next = new RoadSample
            {
                s = s,
                position = position,
                tangent = tangent,
                right = right,
                up = up,
                bankAngle = bankAngle,
                bankTargetAngle = targetBankAngle,
                turnRateDegPerMeter = turnRateDegPerMeter
            };

            samples.Add(next);
        }
    }

    private void EnsureInitialized()
    {
        if (config == null)
        {
            return;
        }

        if (sampleDistance <= 0f)
        {
            sampleDistance = config.chunkLength / Mathf.Max(2, config.samplesPerChunk);
        }

        if (samples.Count > 0)
        {
            return;
        }

        RoadSample first = new RoadSample
        {
            s = 0f,
            position = transform.position,
            tangent = transform.forward.normalized,
            up = transform.up.normalized,
            right = transform.right.normalized,
            bankAngle = 0f,
            bankTargetAngle = 0f,
            turnRateDegPerMeter = 0f
        };
        samples.Add(first);
    }

    private float GetTurnRateDegPerMeter(float s)
    {
        float x = (config.seed * 0.0007f) + s * config.curvatureFrequency;

        float n1 = Mathf.PerlinNoise(x, 0.17f) * 2f - 1f;
        float n2 = Mathf.PerlinNoise(x * 2.13f + 19.1f, 0.73f) * 2f - 1f;
        float blended = n1 * 0.7f + n2 * 0.3f;

        return blended * config.maxTurnRateDegPerMeter;
    }

    private float EstimatePlayerS()
    {
        if (target == null || samples.Count == 0)
        {
            return 0f;
        }

        Vector3 targetPos = target.position;
        float bestDist = float.MaxValue;
        float bestS = 0f;

        for (int i = 0; i < samples.Count; i++)
        {
            float dist = (samples[i].position - targetPos).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestS = samples[i].s;
            }
        }

        return bestS;
    }

    private void CullChunksOutside(int minChunk, int maxChunk)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        List<int> toRemove = ListPool<int>.Get();

        foreach (KeyValuePair<int, ChunkData> pair in chunks)
        {
            if (pair.Key < minChunk || pair.Key > maxChunk)
            {
                toRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            int key = toRemove[i];
            if (!chunks.TryGetValue(key, out ChunkData chunk))
            {
                continue;
            }

            if (chunk.gameObject != null)
            {
                Destroy(chunk.gameObject);
            }

            chunks.Remove(key);
        }

        ListPool<int>.Release(toRemove);
    }

    private void ClearChunks()
    {
        foreach (KeyValuePair<int, ChunkData> pair in chunks)
        {
            if (pair.Value.gameObject != null)
            {
                Destroy(pair.Value.gameObject);
            }
        }

        chunks.Clear();
    }

    private void LogSmoothnessDiagnostics()
    {
        if (config == null || !config.logSmoothnessDiagnostics || samples.Count < 4)
        {
            return;
        }

        float maxStepError = 0f;
        float maxTangentDeltaDeg = 0f;
        float maxBankStepDeg = 0f;
        float maxSeamKinkDeg = 0f;
        int samplesPerChunk = Mathf.Max(2, config.samplesPerChunk);

        for (int i = 1; i < samples.Count; i++)
        {
            float step = Vector3.Distance(samples[i - 1].position, samples[i].position);
            maxStepError = Mathf.Max(maxStepError, Mathf.Abs(step - sampleDistance));

            float tangentDelta = Vector3.Angle(samples[i - 1].tangent, samples[i].tangent);
            maxTangentDeltaDeg = Mathf.Max(maxTangentDeltaDeg, tangentDelta);

            float bankStep = Mathf.Abs(samples[i].bankAngle - samples[i - 1].bankAngle);
            maxBankStepDeg = Mathf.Max(maxBankStepDeg, bankStep);
        }

        for (int i = samplesPerChunk; i < samples.Count - samplesPerChunk; i += samplesPerChunk)
        {
            Vector3 a = (samples[i].position - samples[i - 1].position).normalized;
            Vector3 b = (samples[i + 1].position - samples[i].position).normalized;
            float kink = Vector3.Angle(a, b);
            maxSeamKinkDeg = Mathf.Max(maxSeamKinkDeg, kink);
        }

        Debug.Log(
            $"[RoadStream] Smoothness | samples={samples.Count} | maxStepErr={maxStepError:F4}m | " +
            $"maxTangentDelta={maxTangentDeltaDeg:F3}deg | maxBankStep={maxBankStepDeg:F3}deg | " +
            $"maxSeamKink={maxSeamKinkDeg:F3}deg"
        );

        if (maxSeamKinkDeg > config.seamKinkWarningDeg)
        {
            Debug.LogWarning(
                $"[RoadStream] Seam kink {maxSeamKinkDeg:F3}deg exceeds warning threshold {config.seamKinkWarningDeg:F3}deg."
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (config == null || !config.drawGizmos || samples.Count < 2)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        for (int i = 1; i < samples.Count; i++)
        {
            Gizmos.DrawLine(samples[i - 1].position, samples[i].position);
        }

        int stride = Mathf.Max(1, config.gizmoFrameStride);
        float length = Mathf.Max(0.1f, config.gizmoFrameLength);

        for (int i = 0; i < samples.Count; i += stride)
        {
            RoadSample sample = samples[i];

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(sample.position, sample.position + sample.right * length);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(sample.position, sample.position + sample.up * length);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(sample.position, sample.position + sample.tangent * length);
        }
    }

    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> pool = new Stack<List<T>>();

        public static List<T> Get()
        {
            return pool.Count > 0 ? pool.Pop() : new List<T>();
        }

        public static void Release(List<T> list)
        {
            list.Clear();
            pool.Push(list);
        }
    }
}
