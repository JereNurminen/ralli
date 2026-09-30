using System;
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
        public float slopeAngleDeg;
        public bool isDesignedPiece;
    }

    private class ChunkData
    {
        public int chunkIndex;
        public int sampleStartIndex;
        public int sampleEndIndex;
        public GameObject gameObject;
        public readonly List<GameObject> railObjects = new List<GameObject>();
    }

    private class ChunkLayout
    {
        public int chunkIndex;
        public int sampleStartIndex;
        public int sampleEndIndex;
        public int sampleCount;
        public float startS;
        public float endS;
        public float length;
    }

    private struct ProfilePoint
    {
        public float lateral;
        public float drop;
        public Color color;
    }

    [Header("References")]
    [SerializeField] private RoadGenerationConfig config;
    [SerializeField] private Transform target;
    [SerializeField] private Material roadMaterial;
    [SerializeField] private Material railMaterial;
    [SerializeField] private PhysicsMaterial railPhysicsMaterial;

    [Header("Runtime")]
    [SerializeField] private bool generateOnStart = true;

    private readonly List<RoadSample> samples = new List<RoadSample>(4096);
    private readonly Dictionary<int, ChunkData> chunks = new Dictionary<int, ChunkData>();
    private readonly List<ChunkLayout> chunkLayouts = new List<ChunkLayout>(256);
    // Sample indices bucketed by horizontal position, for "nearest road" queries from terrain.
    private readonly Dictionary<long, List<int>> sampleBuckets = new Dictionary<long, List<int>>();
    private const float SampleBucketSize = 16f;
    private TerrainHeightField heightField;
    private int generation;
    private Material runtimeRailFallbackMaterial;

    private enum PieceType { Straight, Curve, Designed }

    private float sampleDistance;
    private int pieceIndex = -1;
    private float pieceStartS;
    private float pieceEndS;
    private float pieceTurnRateDegPerMeter;
    private float previousCurveTurnRateDegPerMeter;

    private PieceType currentPieceType = PieceType.Straight;
    private DesignedRoadPiece currentDesignedPiece;
    private bool currentDesignedPieceMirrored;
    private float proceduralDistanceSinceLastDesigned;
    private float cumulativeYawDeg;
    private float designedElevationOffset;
    private int lastActiveMinChunk;
    private int lastActiveMaxChunk;

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
            sampleDistance = GetBaseChunkLength() / Mathf.Max(2, config.samplesPerChunk);
        }

        float playerS = EstimatePlayerS();
        int playerChunk = GetChunkIndexAtS(playerS);

        int minChunk = playerChunk - Mathf.Max(0, config.chunksBehind);
        int maxChunk = playerChunk + Mathf.Max(1, config.chunksAhead);
        lastActiveMinChunk = minChunk;
        lastActiveMaxChunk = maxChunk;

        EnsureChunkRange(minChunk, maxChunk);
        CullChunksOutside(minChunk, maxChunk);
    }

    [ContextMenu("Rebuild Road")]
    public void RebuildFromScratch()
    {
        ClearChunks();
        samples.Clear();
        sampleBuckets.Clear();
        heightField = null;
        generation++;
        chunkLayouts.Clear();
        ResetPieceState();

        if (config == null)
        {
            return;
        }

        EnsureInitialized();

        EnsureChunkRange(0, Mathf.Max(1, config.chunksAhead));
        lastActiveMinChunk = 0;
        lastActiveMaxChunk = Mathf.Max(1, config.chunksAhead);
        LogSmoothnessDiagnostics();
    }

    public bool TryGetActiveChunkRange(out int minChunk, out int maxChunk)
    {
        minChunk = lastActiveMinChunk;
        maxChunk = lastActiveMaxChunk;
        return config != null;
    }

    public bool TryGetChunkRangeS(int chunkIndex, out float startS, out float endS)
    {
        startS = 0f;
        endS = 0f;
        if (config == null || chunkIndex < 0)
        {
            return false;
        }

        ChunkLayout layout = GetChunkLayout(chunkIndex);
        startS = layout.startS;
        endS = layout.endS;
        return endS > startS;
    }

    public bool TryGetRoadFrameAtS(float s, out Vector3 position, out Vector3 forward, out Vector3 right, out Vector3 up, out float turnRateDegPerMeter)
    {
        position = transform.position;
        forward = transform.forward;
        right = transform.right;
        up = transform.up;
        turnRateDegPerMeter = 0f;

        if (config == null)
        {
            return false;
        }

        EnsureInitialized();
        if (sampleDistance <= 0.0001f)
        {
            return false;
        }

        float clampedS = Mathf.Max(0f, s);
        int baseIndex = Mathf.Max(0, Mathf.FloorToInt(clampedS / sampleDistance));
        EnsureSamplesUpToIndex(baseIndex + 1);

        int i0 = Mathf.Clamp(baseIndex, 0, samples.Count - 1);
        int i1 = Mathf.Clamp(i0 + 1, 0, samples.Count - 1);
        RoadSample a = samples[i0];
        RoadSample b = samples[i1];

        float segmentLength = Mathf.Max(0.0001f, b.s - a.s);
        float t = Mathf.Clamp01((clampedS - a.s) / segmentLength);

        position = Vector3.Lerp(a.position, b.position, t);
        forward = Vector3.Slerp(a.tangent, b.tangent, t).normalized;
        right = Vector3.Slerp(a.right, b.right, t).normalized;
        up = Vector3.Slerp(a.up, b.up, t).normalized;
        turnRateDegPerMeter = Mathf.Lerp(a.turnRateDegPerMeter, b.turnRateDegPerMeter, t);
        return true;
    }

    public float GetRoadWidth()
    {
        return config != null ? Mathf.Max(0f, config.roadWidth) : 0f;
    }

    public int GetSeed()
    {
        return config != null ? config.seed : 0;
    }

    public int GetChunkIndexForS(float s)
    {
        return GetChunkIndexAtS(s);
    }

    public float GetEstimatedPlayerS()
    {
        return EstimatePlayerS();
    }

    // Bumps whenever the road is rebuilt from scratch; terrain uses it to throw away stale tiles.
    public int Generation => generation;
    public int SampleCount => samples.Count;
    public float SampleSpacing => sampleDistance;
    public bool IsReady => config != null && samples.Count > 0 && heightField != null;
    public RoadGenerationConfig Config => config;
    public TerrainHeightField HeightField => heightField;

    // Distance from centerline to the ditch outer lip, where terrain takes over.
    public float CorridorHalfWidth => config == null
        ? 0f
        : config.roadWidth * 0.5f + Mathf.Max(0f, config.shoulderWidth) + Mathf.Max(0f, config.ditchWidth);

    public Vector3 GetSamplePosition(int index)
    {
        return samples[index].position;
    }

    public Vector3 GetTargetBearingDirection()
    {
        if (config == null || samples.Count == 0)
        {
            return Vector3.forward;
        }

        float startYaw = Mathf.Atan2(samples[0].tangent.x, samples[0].tangent.z) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, startYaw + config.targetBearing, 0f) * Vector3.forward;
    }

    // Nearest road sample within maxDistance (horizontal). lipHeight is the height of the ditch
    // outer lip on that side of the road, following the road's bank.
    public bool TryGetRoadProximity(float x, float z, float maxDistance, out float distance, out float lipHeight)
    {
        distance = float.MaxValue;
        lipHeight = 0f;
        if (config == null || samples.Count == 0)
        {
            return false;
        }

        int cellRadius = Mathf.CeilToInt(maxDistance / SampleBucketSize);
        int cx = Mathf.FloorToInt(x / SampleBucketSize);
        int cz = Mathf.FloorToInt(z / SampleBucketSize);
        float bestSq = maxDistance * maxDistance;
        int bestIndex = -1;

        for (int dx = -cellRadius; dx <= cellRadius; dx++)
        {
            for (int dz = -cellRadius; dz <= cellRadius; dz++)
            {
                if (!sampleBuckets.TryGetValue(GetBucketKey(cx + dx, cz + dz), out List<int> bucket))
                {
                    continue;
                }

                for (int i = 0; i < bucket.Count; i++)
                {
                    Vector3 p = samples[bucket[i]].position;
                    float sq = (p.x - x) * (p.x - x) + (p.z - z) * (p.z - z);
                    if (sq < bestSq)
                    {
                        bestSq = sq;
                        bestIndex = bucket[i];
                    }
                }
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        distance = Mathf.Sqrt(bestSq);
        lipHeight = GetLipHeight(bestIndex, x, z);
        return true;
    }

    // Height of the ditch outer lip on the side of the road where (x, z) lies, following the bank.
    public float GetLipHeight(int sampleIndex, float x, float z)
    {
        RoadSample sample = samples[sampleIndex];
        Vector2 rightFlat = new Vector2(sample.right.x, sample.right.z);
        float flatLength = Mathf.Max(0.0001f, rightFlat.magnitude);
        float lateral = ((x - sample.position.x) * rightFlat.x + (z - sample.position.z) * rightFlat.y) / flatLength;
        float bankSlope = sample.right.y / flatLength;
        float corridor = CorridorHalfWidth;
        return sample.position.y + Mathf.Clamp(lateral, -corridor, corridor) * bankSlope + config.forestFloorYOffset;
    }

    // All sample indices whose position lies inside the given horizontal rectangle.
    public void CollectSampleIndices(float minX, float minZ, float maxX, float maxZ, List<int> result)
    {
        int minCx = Mathf.FloorToInt(minX / SampleBucketSize);
        int maxCx = Mathf.FloorToInt(maxX / SampleBucketSize);
        int minCz = Mathf.FloorToInt(minZ / SampleBucketSize);
        int maxCz = Mathf.FloorToInt(maxZ / SampleBucketSize);
        for (int cx = minCx; cx <= maxCx; cx++)
        {
            for (int cz = minCz; cz <= maxCz; cz++)
            {
                if (!sampleBuckets.TryGetValue(GetBucketKey(cx, cz), out List<int> bucket))
                {
                    continue;
                }

                for (int i = 0; i < bucket.Count; i++)
                {
                    Vector3 p = samples[bucket[i]].position;
                    if (p.x >= minX && p.x <= maxX && p.z >= minZ && p.z <= maxZ)
                    {
                        result.Add(bucket[i]);
                    }
                }
            }
        }
    }

    private void AddSampleToBucket(int sampleIndex)
    {
        Vector3 p = samples[sampleIndex].position;
        long key = GetBucketKey(Mathf.FloorToInt(p.x / SampleBucketSize), Mathf.FloorToInt(p.z / SampleBucketSize));
        if (!sampleBuckets.TryGetValue(key, out List<int> bucket))
        {
            bucket = new List<int>(16);
            sampleBuckets[key] = bucket;
        }

        bucket.Add(sampleIndex);
    }

    private static long GetBucketKey(int cx, int cz)
    {
        return ((long)cx << 32) ^ (uint)cz;
    }

    // Tightest turn (deg/m) the corridor mesh can take without its inner edge folding.
    private float GetMaxSafeTurnRate()
    {
        float minRadius = Mathf.Max(1f, CorridorHalfWidth * Mathf.Max(1f, config.corridorRadiusMargin));
        return Mathf.Rad2Deg / minRadius;
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
        ChunkLayout layout = GetChunkLayout(chunkIndex);
        int startSampleIndex = layout.sampleStartIndex;
        int endSampleIndex = layout.sampleEndIndex;

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
            if (roadMaterial.HasProperty("_RoadHalfWidth"))
            {
                roadMaterial.SetFloat("_RoadHalfWidth", config.roadWidth * 0.5f);
            }
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
        CreateRailObjectsForChunk(chunk, layout);

        chunks[chunkIndex] = chunk;
    }

    private Mesh BuildChunkVisualMesh(int chunkIndex, int startSampleIndex, int endSampleIndex)
    {
        int usableStart = startSampleIndex;
        int usableCount = Mathf.Max(2, (endSampleIndex - usableStart) + 1);
        float halfWidth = config.roadWidth * 0.5f;
        float thickness = Mathf.Max(0.05f, config.roadThickness);
        ProfilePoint[] profile = BuildProfile(halfWidth);
        int profileCount = profile.Length;
        int stride = profileCount * 2; // top + bottom

        Vector3[] vertices = new Vector3[usableCount * stride];
        Vector3[] normals = new Vector3[usableCount * stride];
        Color[] colors = new Color[usableCount * stride];
        Vector2[] uv = new Vector2[usableCount * stride];
        Vector2[] profileNormals = BuildProfileNormals(profile);

        int topStripTriangles = (usableCount - 1) * (profileCount - 1) * 6;
        int bottomStripTriangles = topStripTriangles;
        int sideTriangles = (usableCount - 1) * 12; // left + right
        int[] triangles = new int[topStripTriangles + bottomStripTriangles + sideTriangles];

        for (int i = 0; i < usableCount; i++)
        {
            RoadSample sample = samples[usableStart + i];
            int rowBase = i * stride;

            for (int j = 0; j < profileCount; j++)
            {
                ProfilePoint point = profile[j];
                Vector3 top = sample.position + sample.right * point.lateral - sample.up * point.drop;
                Vector3 bottom = top - sample.up * thickness;
                // Encode road-space UVs for procedural markings:
                // x = lateral offset from centerline (meters), y = distance along road (meters).
                float u = point.lateral;
                float v = sample.s;

                int topIndex = rowBase + j;
                int bottomIndex = rowBase + profileCount + j;

                vertices[topIndex] = top;
                vertices[bottomIndex] = bottom;
                Vector3 topNormal = (sample.right * profileNormals[j].x + sample.up * profileNormals[j].y).normalized;
                normals[topIndex] = topNormal;
                normals[bottomIndex] = -topNormal;
                colors[topIndex] = point.color;
                colors[bottomIndex] = point.color;
                uv[topIndex] = new Vector2(u, v);
                uv[bottomIndex] = new Vector2(u, v);
            }
        }

        int t = 0;
        for (int i = 0; i < usableCount - 1; i++)
        {
            int aRow = i * stride;
            int bRow = (i + 1) * stride;

            for (int j = 0; j < profileCount - 1; j++)
            {
                int a0 = aRow + j;
                int a1 = aRow + j + 1;
                int b0 = bRow + j;
                int b1 = bRow + j + 1;

                // Top strip
                triangles[t++] = a0;
                triangles[t++] = b0;
                triangles[t++] = a1;
                triangles[t++] = a1;
                triangles[t++] = b0;
                triangles[t++] = b1;

                // Bottom strip (reverse winding)
                int a0b = aRow + profileCount + j;
                int a1b = aRow + profileCount + j + 1;
                int b0b = bRow + profileCount + j;
                int b1b = bRow + profileCount + j + 1;
                triangles[t++] = a0b;
                triangles[t++] = a1b;
                triangles[t++] = b0b;
                triangles[t++] = a1b;
                triangles[t++] = b1b;
                triangles[t++] = b0b;
            }

            // Left outer side
            int leftTopA = aRow;
            int leftBottomA = aRow + profileCount;
            int leftTopB = bRow;
            int leftBottomB = bRow + profileCount;
            triangles[t++] = leftTopA;
            triangles[t++] = leftBottomA;
            triangles[t++] = leftTopB;
            triangles[t++] = leftBottomA;
            triangles[t++] = leftBottomB;
            triangles[t++] = leftTopB;

            // Right outer side
            int rightTopA = aRow + profileCount - 1;
            int rightBottomA = aRow + (profileCount * 2) - 1;
            int rightTopB = bRow + profileCount - 1;
            int rightBottomB = bRow + (profileCount * 2) - 1;
            triangles[t++] = rightTopA;
            triangles[t++] = rightTopB;
            triangles[t++] = rightBottomA;
            triangles[t++] = rightBottomA;
            triangles[t++] = rightTopB;
            triangles[t++] = rightBottomB;
        }

        Mesh mesh = new Mesh
        {
            name = $"RoadChunkMesh_{chunkIndex:0000}",
            vertices = vertices,
            normals = normals,
            colors = colors,
            uv = uv,
            triangles = triangles
        };

        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh BuildChunkColliderMesh(int chunkIndex, int startSampleIndex, int endSampleIndex)
    {
        int usableStart = startSampleIndex;
        int usableCount = Mathf.Max(2, (endSampleIndex - usableStart) + 1);
        float halfWidth = config.roadWidth * 0.5f;
        ProfilePoint[] profile = BuildProfile(halfWidth);
        int profileCount = profile.Length;

        Vector3[] vertices = new Vector3[usableCount * profileCount];
        int[] triangles = new int[(usableCount - 1) * (profileCount - 1) * 6];

        for (int i = 0; i < usableCount; i++)
        {
            RoadSample sample = samples[usableStart + i];
            int rowBase = i * profileCount;
            for (int j = 0; j < profileCount; j++)
            {
                ProfilePoint point = profile[j];
                vertices[rowBase + j] = sample.position + sample.right * point.lateral - sample.up * point.drop;
            }
        }

        int t = 0;
        for (int i = 0; i < usableCount - 1; i++)
        {
            int aRow = i * profileCount;
            int bRow = (i + 1) * profileCount;
            for (int j = 0; j < profileCount - 1; j++)
            {
                int a0 = aRow + j;
                int a1 = aRow + j + 1;
                int b0 = bRow + j;
                int b1 = bRow + j + 1;
                triangles[t++] = a0;
                triangles[t++] = b0;
                triangles[t++] = a1;
                triangles[t++] = a1;
                triangles[t++] = b0;
                triangles[t++] = b1;
            }
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

    private void CreateRailObjectsForChunk(ChunkData chunk, ChunkLayout layout)
    {
        if (config == null || chunk == null || chunk.gameObject == null)
        {
            return;
        }

        List<RailSampleMark> marks = BuildRailMarks(layout.sampleStartIndex, layout.sampleEndIndex);
        List<RailSpan> spans = RailPlacementUtility.BuildSpans(marks, Mathf.Max(0f, config.minRailSpanLengthMeters));
        if (spans.Count == 0)
        {
            return;
        }

        float sampleSpacing = Mathf.Max(0.25f, config.railSampleSpacingMeters);
        float halfRoadWidth = Mathf.Max(0f, config.roadWidth) * 0.5f;
        float lateralOffset = Mathf.Max(0f, config.railLateralOffsetMeters);
        float beamCenterHeight = config.railHeightMeters;
        float beamDepth = Mathf.Max(0.02f, config.railBeamDepthMeters);
        float beamHeight = Mathf.Max(0.02f, config.railBeamHeightMeters);
        float beamFlangeThickness = Mathf.Max(0.005f, config.railBeamFlangeThicknessMeters);
        float postWidth = Mathf.Max(0.01f, config.railPostWidthMeters);
        float postSpacing = Mathf.Max(0.25f, config.railPostSpacingMeters);

        for (int i = 0; i < spans.Count; i++)
        {
            RailSpan span = spans[i];
            float edgeOffset = halfRoadWidth + lateralOffset;
            List<RailingFrameSample> frames = BuildRailingFramesForSpan(
                span,
                sampleSpacing,
                edgeOffset,
                beamCenterHeight,
                postWidth);
            Mesh railMesh = RailingMeshBuilder.BuildMesh(
                frames,
                beamDepth,
                beamHeight,
                beamFlangeThickness,
                0f);
            if (railMesh == null)
            {
                continue;
            }

            GameObject railObject = new GameObject($"Rail_{chunk.chunkIndex:0000}_{i:00}_{span.side}");
            railObject.transform.SetParent(chunk.gameObject.transform, true);

            MeshFilter railFilter = railObject.AddComponent<MeshFilter>();
            MeshRenderer railRenderer = railObject.AddComponent<MeshRenderer>();
            MeshCollider railCollider = railObject.AddComponent<MeshCollider>();
            Mesh railColliderMesh = RailingMeshBuilder.BuildRectangularColliderMesh(frames, beamDepth, beamHeight);
            railFilter.sharedMesh = railMesh;
            railCollider.sharedMesh = railColliderMesh != null ? railColliderMesh : railMesh;
            if (railPhysicsMaterial != null)
            {
                railCollider.sharedMaterial = railPhysicsMaterial;
            }
            railRenderer.sharedMaterial = ResolveRailMaterial();
            chunk.railObjects.Add(railObject);

            CreateRailPostsForSpan(chunk, span, edgeOffset, beamCenterHeight, postSpacing);
        }
    }

    private List<RailSampleMark> BuildRailMarks(int startSampleIndex, int endSampleIndex)
    {
        var marks = new List<RailSampleMark>(Mathf.Max(1, endSampleIndex - startSampleIndex + 1));
        int start = Mathf.Clamp(startSampleIndex, 0, samples.Count - 1);
        int end = Mathf.Clamp(endSampleIndex, start, samples.Count - 1);

        for (int i = start; i <= end; i++)
        {
            int prevIndex = Mathf.Max(0, i - 1);
            int nextIndex = Mathf.Min(samples.Count - 1, i + 1);
            RoadSample prev = samples[prevIndex];
            RoadSample next = samples[nextIndex];
            RoadSample current = samples[i];

            float distance = Mathf.Max(0.001f, next.s - prev.s);
            float curvature = RailPlacementUtility.ComputeSignedCurvature(prev.tangent, next.tangent, distance);
            RailSide side = RailPlacementUtility.ClassifySide(
                curvature,
                config.minCurvatureForRail,
                current.isDesignedPiece,
                config.railsOnlyOnDesignedPieces
            );

            marks.Add(new RailSampleMark(current.s, side));
        }

        return marks;
    }

    private List<RailingFrameSample> BuildRailingFramesForSpan(
        RailSpan span,
        float sampleSpacing,
        float edgeOffset,
        float beamCenterHeight,
        float postWidth)
    {
        var frames = new List<RailingFrameSample>();
        float sideSign = span.side == RailSide.Right ? 1f : -1f;

        float endS = span.endS;
        for (float s = span.startS; s <= endS + 0.0001f; s += sampleSpacing)
        {
            if (!TryGetRoadFrameAtS(s, out Vector3 position, out _, out Vector3 right, out Vector3 up, out _))
            {
                continue;
            }

            Vector3 inward = -right * sideSign;
            float localBeamCenterHeight = ComputeRailCenterHeightAtS(span, s, beamCenterHeight);
            Vector3 outerSpineOrigin = position
                                       + right * (edgeOffset * sideSign)
                                       + inward * (postWidth * 0.5f)
                                       + up * localBeamCenterHeight;
            frames.Add(new RailingFrameSample(s, outerSpineOrigin, inward, up));
        }

        if (frames.Count > 0 && frames[frames.Count - 1].s < endS - 0.0001f &&
            TryGetRoadFrameAtS(endS, out Vector3 endPosition, out _, out Vector3 endRight, out Vector3 endUp, out _))
        {
            Vector3 endInward = -endRight * sideSign;
            float endBeamCenterHeight = ComputeRailCenterHeightAtS(span, endS, beamCenterHeight);
            Vector3 endOuterSpine = endPosition
                                    + endRight * (edgeOffset * sideSign)
                                    + endInward * (postWidth * 0.5f)
                                    + endUp * endBeamCenterHeight;
            frames.Add(new RailingFrameSample(endS, endOuterSpine, endInward, endUp));
        }

        return frames;
    }

    private void CreateRailPostsForSpan(ChunkData chunk, RailSpan span, float edgeOffset, float beamCenterHeight, float postSpacing)
    {
        float sideSign = span.side == RailSide.Right ? 1f : -1f;
        float postHeight = Mathf.Max(0.05f, config.railPostHeightMeters);
        float postWidth = Mathf.Max(0.01f, config.railPostWidthMeters);
        float postDepth = Mathf.Max(0.01f, config.railPostDepthMeters);
        float embedDepth = Mathf.Max(0f, config.railPostEmbedDepthMeters);

        int postIndex = 0;
        for (float s = span.startS; s <= span.endS + 0.0001f; s += postSpacing)
        {
            if (!TryGetRoadFrameAtS(s, out Vector3 position, out Vector3 forward, out Vector3 right, out Vector3 up, out _))
            {
                continue;
            }

            float localBeamCenterHeight = ComputeRailCenterHeightAtS(span, s, beamCenterHeight);
            Vector3 postCenter = position
                                 + right * (edgeOffset * sideSign)
                                 + up * (localBeamCenterHeight - postHeight * 0.5f - embedDepth);

            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = $"RailPost_{chunk.chunkIndex:0000}_{postIndex:000}_{span.side}";
            post.transform.SetParent(chunk.gameObject.transform, true);
            post.transform.position = postCenter;
            post.transform.rotation = Quaternion.LookRotation(forward, up);
            post.transform.localScale = new Vector3(postWidth, postHeight, postDepth);

            if (post.TryGetComponent(out BoxCollider postCollider))
            {
                Destroy(postCollider);
            }

            if (post.TryGetComponent(out MeshRenderer postRenderer))
            {
                postRenderer.sharedMaterial = ResolveRailMaterial();
            }

            postIndex++;
        }
    }

    private float ComputeRailCenterHeightAtS(RailSpan span, float s, float beamCenterHeight)
    {
        float beamHalfHeight = Mathf.Max(0.01f, config.railBeamHeightMeters * 0.5f);
        // Existing assets may deserialize new fields as 0; keep a visible default behavior.
        float dropDistance = config.railEndDropDistanceMeters > 0.001f
            ? config.railEndDropDistanceMeters
            : 2.5f;
        float clipDepth = config.railEndGroundClipDepthMeters > 0.0001f
            ? config.railEndGroundClipDepthMeters
            : 0.12f;

        // Sink the full beam below ground at span tips.
        float endCenterHeight = -(config.railBeamHeightMeters + clipDepth);
        float tLinear = RailPlacementUtility.ComputeEndDrop01(s, span.startS, span.endS, dropDistance);
        float tSmoothed = tLinear * tLinear * (3f - 2f * tLinear);
        return Mathf.Lerp(endCenterHeight, beamCenterHeight, tSmoothed);
    }

    // Road corridor cross-section: apron → ditch → shoulder → asphalt → shoulder → ditch → apron.
    // Everything beyond the ditch lip is terrain (TerrainStreamer), so this stays narrow and
    // never folds as long as turn rate respects GetMaxSafeTurnRate().
    private ProfilePoint[] BuildProfile(float halfRoadWidth)
    {
        float shoulderWidth = Mathf.Max(0f, config.shoulderWidth);
        float shoulderDrop = Mathf.Max(0f, config.shoulderDrop);
        float ditchWidth = Mathf.Max(0f, config.ditchWidth);
        float ditchDepth = Mathf.Max(0f, config.ditchDepth);
        float ditchBottomFlatWidth = Mathf.Clamp(config.ditchBottomFlatWidth, 0f, ditchWidth);
        float lipDrop = -config.forestFloorYOffset;
        float apronDepth = Mathf.Max(0f, config.corridorApronDepth);

        Color asphalt = new Color(1f, 0f, 0f, 0f);
        Color dirt = new Color(0f, 1f, 0f, 0f);
        Color forest = new Color(0f, 1f, 1f, 0f);

        float shoulderOuter = halfRoadWidth + shoulderWidth;
        float ditchOuter = shoulderOuter + ditchWidth;
        float ditchSideWidth = Mathf.Max(0f, (ditchWidth - ditchBottomFlatWidth) * 0.5f);
        float ditchBottomInner = shoulderOuter + ditchSideWidth;
        float ditchBottomOuter = ditchOuter - ditchSideWidth;
        float ditchBottomDrop = Mathf.Max(shoulderDrop, lipDrop) + ditchDepth;

        var points = new List<ProfilePoint>(12);

        void AddPoint(float lateral, float drop, Color color)
        {
            if (points.Count > 0 && Mathf.Abs(points[points.Count - 1].lateral - lateral) < 0.0001f
                && Mathf.Abs(points[points.Count - 1].drop - drop) < 0.0001f)
            {
                points[points.Count - 1] = new ProfilePoint { lateral = lateral, drop = drop, color = color };
                return;
            }

            points.Add(new ProfilePoint { lateral = lateral, drop = drop, color = color });
        }

        if (apronDepth > 0.001f)
        {
            AddPoint(-ditchOuter, lipDrop + apronDepth, forest);
        }

        if (ditchWidth > 0.001f)
        {
            AddPoint(-ditchOuter, lipDrop, forest);
            AddPoint(-ditchBottomOuter, ditchBottomDrop, dirt);
            if (ditchBottomFlatWidth > 0.001f)
            {
                AddPoint(-ditchBottomInner, ditchBottomDrop, dirt);
            }
        }

        AddPoint(-shoulderOuter, shoulderDrop, dirt);
        AddPoint(-halfRoadWidth, 0f, asphalt);
        AddPoint(halfRoadWidth, 0f, asphalt);
        AddPoint(shoulderOuter, shoulderDrop, dirt);

        if (ditchWidth > 0.001f)
        {
            if (ditchBottomFlatWidth > 0.001f)
            {
                AddPoint(ditchBottomInner, ditchBottomDrop, dirt);
            }
            AddPoint(ditchBottomOuter, ditchBottomDrop, dirt);
            AddPoint(ditchOuter, lipDrop, forest);
        }

        if (apronDepth > 0.001f)
        {
            AddPoint(ditchOuter, lipDrop + apronDepth, forest);
        }

        return points.ToArray();
    }

    private Material ResolveRailMaterial()
    {
        if (railMaterial != null)
        {
            return railMaterial;
        }

        if (runtimeRailFallbackMaterial != null)
        {
            return runtimeRailFallbackMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        runtimeRailFallbackMaterial = shader != null
            ? new Material(shader)
            : new Material(Shader.Find("Sprites/Default"));
        runtimeRailFallbackMaterial.name = "Runtime_Rail_MatteGrey";
        runtimeRailFallbackMaterial.hideFlags = HideFlags.DontSave;

        // Matte light-grey metal fallback look.
        Color matteGrey = new Color(0.74f, 0.76f, 0.78f, 1f);
        if (runtimeRailFallbackMaterial.HasProperty("_BaseColor"))
        {
            runtimeRailFallbackMaterial.SetColor("_BaseColor", matteGrey);
        }
        if (runtimeRailFallbackMaterial.HasProperty("_Color"))
        {
            runtimeRailFallbackMaterial.SetColor("_Color", matteGrey);
        }
        if (runtimeRailFallbackMaterial.HasProperty("_Metallic"))
        {
            runtimeRailFallbackMaterial.SetFloat("_Metallic", 0.75f);
        }
        if (runtimeRailFallbackMaterial.HasProperty("_Smoothness"))
        {
            runtimeRailFallbackMaterial.SetFloat("_Smoothness", 0.18f);
        }
        if (runtimeRailFallbackMaterial.HasProperty("_Glossiness"))
        {
            runtimeRailFallbackMaterial.SetFloat("_Glossiness", 0.18f);
        }

        return runtimeRailFallbackMaterial;
    }

    private Vector2[] BuildProfileNormals(ProfilePoint[] profile)
    {
        int count = profile.Length;
        Vector2[] normals = new Vector2[count];
        if (count == 0)
        {
            return normals;
        }

        for (int i = 0; i < count; i++)
        {
            int prev = Mathf.Max(0, i - 1);
            int next = Mathf.Min(count - 1, i + 1);
            float dx = profile[next].lateral - profile[prev].lateral;
            float dy = profile[next].drop - profile[prev].drop;
            float slope = Mathf.Abs(dx) > 0.0001f ? dy / dx : 0f;

            // Local 2D normal in (right, up) space for cross-section slope.
            Vector2 n = new Vector2(slope, 1f).normalized;
            normals[i] = n;
        }

        return normals;
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
            float maxSafeTurnRate = GetMaxSafeTurnRate();
            turnRateDegPerMeter = Mathf.Clamp(turnRateDegPerMeter, -maxSafeTurnRate, maxSafeTurnRate);
            float yawDelta = turnRateDegPerMeter * sampleDistance;
            float prevYaw = Mathf.Atan2(prev.tangent.x, prev.tangent.z) * Mathf.Rad2Deg;
            float yaw = prevYaw + yawDelta;
            Vector3 horizontalForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            float slopeAngleDeg = 0f;
            if (config.enableHills)
            {
                float targetSlopeDeg = GetTargetSlopeDeg(prev, s, horizontalForward);
                targetSlopeDeg = Mathf.Clamp(targetSlopeDeg, -Mathf.Abs(config.maxSlopeAngleDeg), Mathf.Abs(config.maxSlopeAngleDeg));
                slopeAngleDeg = Mathf.Lerp(prev.slopeAngleDeg, targetSlopeDeg, Mathf.Clamp01(config.slopeResponse));
            }

            float slopeRad = slopeAngleDeg * Mathf.Deg2Rad;
            Vector3 tangent = (horizontalForward * Mathf.Cos(slopeRad) + Vector3.up * Mathf.Sin(slopeRad)).normalized;

            float absTurnRate = Mathf.Abs(turnRateDegPerMeter);
            float rawBankTarget = 0f;
            if (absTurnRate > config.bankTurnRateDeadzone && config.maxTurnRateDegPerMeter > 0.001f)
            {
                float denom = Mathf.Max(0.001f, config.maxTurnRateDegPerMeter - config.bankTurnRateDeadzone);
                float tCurve = Mathf.Clamp01((absTurnRate - config.bankTurnRateDeadzone) / denom);
                tCurve = tCurve * tCurve * (3f - 2f * tCurve);
                float signed = Mathf.Sign(turnRateDegPerMeter);
                rawBankTarget = -signed * Mathf.Min(config.maxBankAngle, config.bankFromCurvature) * tCurve;
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
                turnRateDegPerMeter = turnRateDegPerMeter,
                slopeAngleDeg = slopeAngleDeg,
                isDesignedPiece = currentPieceType == PieceType.Designed
            };

            samples.Add(next);
            AddSampleToBucket(samples.Count - 1);
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
            sampleDistance = GetBaseChunkLength() / Mathf.Max(2, config.samplesPerChunk);
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
            turnRateDegPerMeter = 0f,
            slopeAngleDeg = 0f,
            isDesignedPiece = false
        };
        samples.Add(first);
        AddSampleToBucket(0);
        heightField = new TerrainHeightField(config, first.position);
        ResetPieceState();
    }

    private float GetTurnRateDegPerMeter(float s)
    {
        while (s >= pieceEndS)
        {
            AdvancePiece();
        }

        if (currentPieceType == PieceType.Designed && currentDesignedPiece != null)
        {
            float pieceLen = pieceEndS - pieceStartS;
            float t = pieceLen > 0f ? Mathf.Clamp01((s - pieceStartS) / pieceLen) : 0f;
            float rate = currentDesignedPiece.turnRateCurve.Evaluate(t);
            if (currentDesignedPieceMirrored) rate = -rate;
            return rate;
        }

        return pieceTurnRateDegPerMeter;
    }

    // Designed pieces with elevation keep their authored shape (followed as slope, so it is
    // relative to wherever the piece starts). Everything else climbs/descends toward the terrain
    // height ahead, plus short bumps, with any offset left by a designed piece fading out.
    private float GetTargetSlopeDeg(RoadSample prev, float s, Vector3 horizontalForward)
    {
        if (currentPieceType == PieceType.Designed && currentDesignedPiece != null && currentDesignedPiece.hasElevation)
        {
            designedElevationOffset = prev.position.y - heightField.GetHeight(prev.position.x, prev.position.z);
            float heightNow = GetDesignedElevation(s);
            float heightPrev = GetDesignedElevation(prev.s);
            return Mathf.Atan2(heightNow - heightPrev, sampleDistance) * Mathf.Rad2Deg;
        }

        designedElevationOffset *= Mathf.Exp(-sampleDistance / Mathf.Max(1f, config.designedElevationOffsetFade));
        Vector3 ahead = prev.position + horizontalForward * sampleDistance;
        float target = heightField.GetHeight(ahead.x, ahead.z) + designedElevationOffset + GetSmallBump(s);
        return Mathf.Atan2(target - prev.position.y, Mathf.Max(1f, config.elevationCatchupDistance)) * Mathf.Rad2Deg;
    }

    private float GetDesignedElevation(float s)
    {
        float pieceLen = pieceEndS - pieceStartS;
        float t = pieceLen > 0f ? Mathf.Clamp01((s - pieceStartS) / pieceLen) : 0f;
        return currentDesignedPiece.elevationCurve.Evaluate(t);
    }

    private float GetSmallBump(float s)
    {
        float smallWavelength = Mathf.Max(6f, config.smallBumpWavelength);
        float bumpPatchLength = Mathf.Max(20f, config.smallBumpPatchLength);
        float seedOffset = config.seed * 0.137f;
        float smallPhase = (s + seedOffset * 17f) * (Mathf.PI * 2f) / smallWavelength;

        float bumpMaskNoise = Mathf.PerlinNoise((s + seedOffset * 97f) / bumpPatchLength, 0.37f);
        float bumpThreshold = 1f - Mathf.Clamp01(config.smallBumpOccurrence);
        float bumpMask = Mathf.InverseLerp(bumpThreshold, 1f, bumpMaskNoise);
        bumpMask = bumpMask * bumpMask * (3f - 2f * bumpMask);

        return Mathf.Sin(smallPhase) * config.smallBumpAmplitude * bumpMask;
    }

    private void ResetPieceState()
    {
        pieceIndex = -1;
        pieceStartS = 0f;
        pieceEndS = 0f;
        pieceTurnRateDegPerMeter = 0f;
        previousCurveTurnRateDegPerMeter = 0f;
        currentPieceType = PieceType.Straight;
        currentDesignedPiece = null;
        currentDesignedPieceMirrored = false;
        proceduralDistanceSinceLastDesigned = 0f;
        cumulativeYawDeg = 0f;
        designedElevationOffset = 0f;
    }

    private void AdvancePiece()
    {
        pieceIndex++;
        pieceStartS = pieceEndS;

        bool forceStartStraight = pieceIndex == 0;

        // Check if we should place a designed piece
        if (!forceStartStraight && config.designedPiecePool != null &&
            config.designedPiecePool.pieces.Count > 0)
        {
            float threshold = Mathf.Lerp(
                Mathf.Max(0f, config.minProceduralBetweenDesigned),
                Mathf.Max(0f, config.maxProceduralBetweenDesigned),
                Hash01(pieceIndex, 100)
            );

            if (proceduralDistanceSinceLastDesigned >= threshold)
            {
                bool mirrored;
                DesignedRoadPiece piece = SelectDesignedPiece(out mirrored);
                if (piece != null && piece.arcLength > 0f)
                {
                    currentPieceType = PieceType.Designed;
                    currentDesignedPiece = piece;
                    currentDesignedPieceMirrored = mirrored;
                    float yawDelta = mirrored ? -piece.totalYawDeltaDeg : piece.totalYawDeltaDeg;
                    cumulativeYawDeg += yawDelta;
                    pieceEndS = pieceStartS + piece.arcLength;
                    pieceTurnRateDegPerMeter = mirrored ? -piece.entryTurnRate : piece.entryTurnRate;
                    proceduralDistanceSinceLastDesigned = 0f;
                    return;
                }
            }
        }

        // Procedural piece (straight or curve)
        currentPieceType = PieceType.Straight;
        currentDesignedPiece = null;
        currentDesignedPieceMirrored = false;

        bool isCurve = !forceStartStraight && Hash01(pieceIndex, 0) < Mathf.Clamp01(config.curvePieceProbability);

        float pieceLength;
        float turnRate = 0f;

        if (isCurve)
        {
            currentPieceType = PieceType.Curve;

            float minCurveLength = Mathf.Max(8f, config.minCurveLength);
            float maxCurveLength = Mathf.Max(minCurveLength, config.maxCurveLength);
            pieceLength = Mathf.Lerp(minCurveLength, maxCurveLength, Hash01(pieceIndex, 1));

            float curveRateCap = Mathf.Max(0.001f, config.maxTurnRateDegPerMeter);
            float minCurveRate = Mathf.Min(Mathf.Max(0.001f, config.minCurveTurnRateDegPerMeter), curveRateCap);
            float maxCurveRateCfg = Mathf.Max(minCurveRate, config.maxCurveTurnRateDegPerMeter);
            float maxCurveRate = Mathf.Min(maxCurveRateCfg, curveRateCap);
            float absRate = Mathf.Lerp(minCurveRate, maxCurveRate, Hash01(pieceIndex, 2));

            float direction = Hash01(pieceIndex, 3) < 0.5f ? -1f : 1f;
            if (Mathf.Abs(previousCurveTurnRateDegPerMeter) > 0.001f)
            {
                float previousDirection = Mathf.Sign(previousCurveTurnRateDegPerMeter);
                bool flipDirection = Hash01(pieceIndex, 4) < Mathf.Clamp01(config.oppositeCurveChance);
                direction = flipDirection ? -previousDirection : previousDirection;
            }

            // Steer back toward the target bearing: the further off, the likelier the correcting direction.
            float headingError = Mathf.DeltaAngle(config.targetBearing, cumulativeYawDeg);
            float correctChance = Mathf.Clamp01(config.headingCorrectionStrength) * Mathf.Clamp01(Mathf.Abs(headingError) / 45f);
            if (Mathf.Abs(headingError) > 0.5f && Hash01(pieceIndex, 6) < correctChance)
            {
                direction = -Mathf.Sign(headingError);
            }

            turnRate = direction * absRate;
            previousCurveTurnRateDegPerMeter = turnRate;
            cumulativeYawDeg += turnRate * pieceLength;
        }
        else
        {
            float minStraightLength = Mathf.Max(10f, config.minStraightLength);
            float maxStraightLength = Mathf.Max(minStraightLength, config.maxStraightLength);
            pieceLength = Mathf.Lerp(minStraightLength, maxStraightLength, Hash01(pieceIndex, 5));
        }

        pieceEndS = pieceStartS + Mathf.Max(sampleDistance, pieceLength);
        pieceTurnRateDegPerMeter = turnRate;
        proceduralDistanceSinceLastDesigned += pieceLength;
    }

    private DesignedRoadPiece SelectDesignedPiece(out bool mirrored)
    {
        mirrored = false;
        var pool = config.designedPiecePool;
        if (pool == null || pool.pieces.Count == 0)
            return null;

        float headingError = Mathf.DeltaAngle(config.targetBearing, cumulativeYawDeg);
        float strength = Mathf.Clamp01(config.headingCorrectionStrength);

        // Build weighted candidate list (each entry + optional mirror = up to 2 candidates per entry)
        // To keep it simple and allocation-free, do two passes: compute total weight, then select.
        float totalWeight = 0f;
        int entryCount = pool.pieces.Count;

        for (int i = 0; i < entryCount; i++)
        {
            var entry = pool.pieces[i];
            if (entry.piece == null || entry.piece.arcLength <= 0f || entry.weight <= 0f)
                continue;

            totalWeight += ScoreCandidate(entry.weight, entry.piece.totalYawDeltaDeg, headingError, strength);

            if (entry.canMirror)
                totalWeight += ScoreCandidate(entry.weight, -entry.piece.totalYawDeltaDeg, headingError, strength);
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Hash01(pieceIndex, 200) * totalWeight;
        float accumulated = 0f;

        for (int i = 0; i < entryCount; i++)
        {
            var entry = pool.pieces[i];
            if (entry.piece == null || entry.piece.arcLength <= 0f || entry.weight <= 0f)
                continue;

            // Normal orientation
            accumulated += ScoreCandidate(entry.weight, entry.piece.totalYawDeltaDeg, headingError, strength);
            if (roll <= accumulated)
            {
                mirrored = false;
                return entry.piece;
            }

            // Mirrored orientation
            if (entry.canMirror)
            {
                accumulated += ScoreCandidate(entry.weight, -entry.piece.totalYawDeltaDeg, headingError, strength);
                if (roll <= accumulated)
                {
                    mirrored = true;
                    return entry.piece;
                }
            }
        }

        // Fallback (floating-point drift): return first valid piece
        for (int i = 0; i < entryCount; i++)
        {
            var entry = pool.pieces[i];
            if (entry.piece != null && entry.piece.arcLength > 0f && entry.weight > 0f)
            {
                Debug.LogWarning("[RoadStream] Weighted selection fallback triggered.");
                return entry.piece;
            }
        }

        return null;
    }

    private float ScoreCandidate(float baseWeight, float yawDelta, float headingError, float strength)
    {
        // Positive correction score when piece steers toward target bearing
        float errorAfter = Mathf.Abs(headingError + yawDelta);
        float errorBefore = Mathf.Abs(headingError);
        float correctionScore = (errorBefore - errorAfter) * 0.01f;
        return Mathf.Max(0.001f, baseWeight * (1f + strength * correctionScore));
    }

    private float Hash01(int index, int salt)
    {
        unchecked
        {
            uint x = (uint)config.seed;
            x ^= (uint)(index + 1) * 747796405u;
            x ^= (uint)(salt + 17) * 2891336453u;
            x ^= x >> 16;
            x *= 2246822519u;
            x ^= x >> 13;
            x *= 3266489917u;
            x ^= x >> 16;
            return (x & 0x00FFFFFFu) / 16777215f;
        }
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

    private float GetBaseChunkLength()
    {
        float fallback = Mathf.Max(20f, config.chunkLength);
        float min = config.minChunkLength > 0f ? config.minChunkLength : fallback;
        float max = config.maxChunkLength > 0f ? config.maxChunkLength : fallback;
        min = Mathf.Max(20f, min);
        max = Mathf.Max(min, max);
        return 0.5f * (min + max);
    }

    private float GetChunkLengthForIndex(int chunkIndex)
    {
        float fallback = Mathf.Max(20f, config.chunkLength);
        float min = config.minChunkLength > 0f ? config.minChunkLength : fallback;
        float max = config.maxChunkLength > 0f ? config.maxChunkLength : fallback;
        min = Mathf.Max(20f, min);
        max = Mathf.Max(min, max);
        return Mathf.Lerp(min, max, Hash01(chunkIndex, 101));
    }

    private void EnsureChunkLayoutsUpToIndex(int chunkIndex)
    {
        if (chunkIndex < 0)
        {
            return;
        }

        while (chunkLayouts.Count <= chunkIndex)
        {
            int index = chunkLayouts.Count;
            ChunkLayout previous = index > 0 ? chunkLayouts[index - 1] : null;
            int sampleStartIndex = previous == null ? 0 : previous.sampleEndIndex;
            float startS = previous == null ? 0f : previous.endS;

            float chunkLength = GetChunkLengthForIndex(index);
            int sampleCount = Mathf.Max(2, Mathf.RoundToInt(chunkLength / Mathf.Max(0.01f, sampleDistance)));
            int sampleEndIndex = sampleStartIndex + sampleCount;
            float endS = startS + sampleCount * sampleDistance;

            chunkLayouts.Add(new ChunkLayout
            {
                chunkIndex = index,
                sampleStartIndex = sampleStartIndex,
                sampleEndIndex = sampleEndIndex,
                sampleCount = sampleCount,
                startS = startS,
                endS = endS,
                length = chunkLength
            });
        }
    }

    private ChunkLayout GetChunkLayout(int chunkIndex)
    {
        EnsureChunkLayoutsUpToIndex(chunkIndex);
        return chunkLayouts[chunkIndex];
    }

    private int GetChunkIndexAtS(float s)
    {
        if (s <= 0f)
        {
            return 0;
        }

        while (chunkLayouts.Count == 0 || chunkLayouts[chunkLayouts.Count - 1].endS <= s)
        {
            EnsureChunkLayoutsUpToIndex(chunkLayouts.Count);
        }

        int lo = 0;
        int hi = chunkLayouts.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            ChunkLayout layout = chunkLayouts[mid];
            if (s < layout.startS)
            {
                hi = mid - 1;
            }
            else if (s >= layout.endS)
            {
                lo = mid + 1;
            }
            else
            {
                return layout.chunkIndex;
            }
        }

        return Mathf.Max(0, lo - 1);
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

        for (int i = 1; i < samples.Count; i++)
        {
            float step = Vector3.Distance(samples[i - 1].position, samples[i].position);
            maxStepError = Mathf.Max(maxStepError, Mathf.Abs(step - sampleDistance));

            float tangentDelta = Vector3.Angle(samples[i - 1].tangent, samples[i].tangent);
            maxTangentDeltaDeg = Mathf.Max(maxTangentDeltaDeg, tangentDelta);

            float bankStep = Mathf.Abs(samples[i].bankAngle - samples[i - 1].bankAngle);
            maxBankStepDeg = Mathf.Max(maxBankStepDeg, bankStep);
        }

        if (chunkLayouts.Count > 1)
        {
            for (int i = 1; i < chunkLayouts.Count; i++)
            {
                int seamIndex = chunkLayouts[i].sampleStartIndex;
                if (seamIndex <= 0 || seamIndex + 1 >= samples.Count)
                {
                    continue;
                }

                Vector3 a = (samples[seamIndex].position - samples[seamIndex - 1].position).normalized;
                Vector3 b = (samples[seamIndex + 1].position - samples[seamIndex].position).normalized;
                float kink = Vector3.Angle(a, b);
                maxSeamKinkDeg = Mathf.Max(maxSeamKinkDeg, kink);
            }
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

        DrawSeamMarkers();
    }

    private void DrawSeamMarkers()
    {
        if (config == null || !config.drawSeamMarkers || samples.Count < 2)
        {
            return;
        }

        float radius = Mathf.Max(0.05f, config.seamMarkerRadius);
        float height = Mathf.Max(0.2f, config.seamMarkerHeight);
        Color seamColor = new Color(1f, 0.2f, 0.2f, 0.9f);

        if (chunkLayouts.Count <= 1)
        {
            return;
        }

        for (int i = 1; i < chunkLayouts.Count; i++)
        {
            int seamSampleIndex = chunkLayouts[i].sampleStartIndex;
            if (seamSampleIndex <= 0 || seamSampleIndex >= samples.Count)
            {
                continue;
            }

            RoadSample seam = samples[seamSampleIndex];
            Vector3 basePos = seam.position + seam.up * 0.06f;
            Vector3 topPos = basePos + seam.up * height;

            Gizmos.color = seamColor;
            Gizmos.DrawSphere(basePos, radius);
            Gizmos.DrawLine(basePos, topPos);
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
