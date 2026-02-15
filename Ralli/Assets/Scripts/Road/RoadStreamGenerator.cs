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
    }

    private class ChunkData
    {
        public int chunkIndex;
        public int sampleStartIndex;
        public int sampleEndIndex;
        public GameObject gameObject;
    }

    private struct ForestTriangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public float cumulativeArea;
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
    [SerializeField] private GameObject[] birchTreePrefabs;
    [SerializeField] private GameObject[] pineTreePrefabs;
    [SerializeField] private Material birchLeafFallbackMaterial;
    [SerializeField] private Material pineLeafFallbackMaterial;
    [SerializeField] private Material birchBarkFallbackMaterial;
    [SerializeField] private Material pineBarkFallbackMaterial;

    [Header("Runtime")]
    [SerializeField] private bool generateOnStart = true;

    private readonly List<RoadSample> samples = new List<RoadSample>(4096);
    private readonly Dictionary<int, ChunkData> chunks = new Dictionary<int, ChunkData>();
    private readonly List<ChunkLayout> chunkLayouts = new List<ChunkLayout>(256);
    private readonly Dictionary<int, List<Vector2>> chunkTreePositions = new Dictionary<int, List<Vector2>>();

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

        EnsureChunkRange(minChunk, maxChunk);
        CullChunksOutside(minChunk, maxChunk);
    }

    [ContextMenu("Rebuild Road")]
    public void RebuildFromScratch()
    {
        ClearChunks();
        samples.Clear();
        chunkLayouts.Clear();
        ResetPieceState();

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
        SpawnTreesForChunk(chunkIndex, chunkObject, visualMesh, startSampleIndex, endSampleIndex);

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
                float adjustedLateral = AdjustLateralForCurvature(point.lateral, sample.turnRateDegPerMeter);
                Vector3 top = sample.position + sample.right * adjustedLateral - sample.up * point.drop;
                Vector3 bottom = top - sample.up * thickness;
                // Encode road-space UVs for procedural markings:
                // x = lateral offset from centerline (meters), y = distance along road (meters).
                float u = adjustedLateral;
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
                float adjustedLateral = AdjustLateralForCurvature(point.lateral, sample.turnRateDegPerMeter);
                vertices[rowBase + j] = sample.position + sample.right * adjustedLateral - sample.up * point.drop;
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

    private ProfilePoint[] BuildProfile(float halfRoadWidth)
    {
        float shoulderWidth = Mathf.Max(0f, config.shoulderWidth);
        float shoulderDrop = Mathf.Max(0f, config.shoulderDrop);
        float ditchWidth = Mathf.Max(0f, config.ditchWidth);
        float ditchDepth = Mathf.Max(0f, config.ditchDepth);
        float ditchBottomFlatWidth = Mathf.Clamp(config.ditchBottomFlatWidth, 0f, ditchWidth);
        float dropSkirtDepth = Mathf.Max(0f, config.dropSkirtDepth);
        float forestFloorDrop = -config.forestFloorYOffset;
        float collidableForestWidth = Mathf.Max(0f, config.collidableForestWidth);

        Color asphalt = new Color(1f, 0f, 0f, 0f);
        Color dirt = new Color(0f, 1f, 0f, 0f);
        Color forest = new Color(0f, 1f, 1f, 0f);

        bool hasEdge = shoulderWidth > 0.001f || ditchWidth > 0.001f
                       || collidableForestWidth > 0.001f || dropSkirtDepth > 0.001f;

        if (!hasEdge)
        {
            return new[]
            {
                new ProfilePoint { lateral = -halfRoadWidth, drop = 0f, color = asphalt },
                new ProfilePoint { lateral = halfRoadWidth, drop = 0f, color = asphalt }
            };
        }

        float shoulderOuter = halfRoadWidth + shoulderWidth;
        float ditchOuter = shoulderOuter + ditchWidth;
        float forestOuter = ditchOuter + collidableForestWidth;
        float ditchSideWidth = Mathf.Max(0f, (ditchWidth - ditchBottomFlatWidth) * 0.5f);
        float ditchBottomInner = shoulderOuter + ditchSideWidth;
        float ditchBottomOuter = ditchOuter - ditchSideWidth;
        float ditchBottomDrop = Mathf.Max(shoulderDrop, forestFloorDrop) + ditchDepth;

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

        // Left side: skirt bottom → forest outer edge → ditch → shoulder
        if (dropSkirtDepth > 0.001f)
        {
            AddPoint(-forestOuter, forestFloorDrop + dropSkirtDepth, forest);
        }

        if (collidableForestWidth > 0.001f)
        {
            AddPoint(-forestOuter, forestFloorDrop, forest);
        }

        if (ditchWidth > 0.001f)
        {
            AddPoint(-ditchOuter, forestFloorDrop, dirt);
            AddPoint(-ditchBottomOuter, ditchBottomDrop, dirt);
            if (ditchBottomFlatWidth > 0.001f)
            {
                AddPoint(-ditchBottomInner, ditchBottomDrop, dirt);
            }
        }

        if (hasEdge)
        {
            AddPoint(-shoulderOuter, shoulderDrop, dirt);
        }

        AddPoint(-halfRoadWidth, 0f, asphalt);
        AddPoint(halfRoadWidth, 0f, asphalt);

        // Right side: shoulder → ditch → forest outer edge → skirt bottom
        if (hasEdge)
        {
            AddPoint(shoulderOuter, shoulderDrop, dirt);
        }

        if (ditchWidth > 0.001f)
        {
            if (ditchBottomFlatWidth > 0.001f)
            {
                AddPoint(ditchBottomInner, ditchBottomDrop, dirt);
            }
            AddPoint(ditchBottomOuter, ditchBottomDrop, dirt);
            AddPoint(ditchOuter, forestFloorDrop, dirt);
        }

        if (collidableForestWidth > 0.001f)
        {
            AddPoint(forestOuter, forestFloorDrop, forest);
        }

        if (dropSkirtDepth > 0.001f)
        {
            AddPoint(forestOuter, forestFloorDrop + dropSkirtDepth, forest);
        }

        return points.ToArray();
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

    private float AdjustLateralForCurvature(float lateral, float turnRateDegPerMeter)
    {
        float turnRateRad = Mathf.Abs(turnRateDegPerMeter) * Mathf.Deg2Rad;
        if (turnRateRad < 0.0001f)
            return lateral;

        float radius = 1f / turnRateRad;

        // On a curve with radius R, a point at lateral offset d from the centerline
        // traces an arc of radius (R - d) on the inside, or (R + d) on the outside.
        // When (R - d) gets small, consecutive sample strips overlap on the inside.
        //
        // Positive turn rate = turning left = inside is at positive lateral (right side).
        float insideSign = Mathf.Sign(turnRateDegPerMeter);
        float insideLateral = lateral * insideSign;

        if (insideLateral <= 0f)
            return lateral; // Outside of curve — no issue.

        // Don't compress the road surface itself (asphalt + shoulder).
        float safeZone = config.roadWidth * 0.5f + config.shoulderWidth;
        if (insideLateral <= safeZone)
            return lateral;

        // Beyond the safe zone, compress toward safeZone based on curvature.
        // The inner profile must not extend past ~half the radius, because on a
        // hairpin the returning road occupies the space beyond that.
        float radiusFactor = Mathf.Clamp(config.innerProfileCompressionRadiusFactor, 0.4f, 0.95f);
        float maxExtent = Mathf.Max(0f, radius * radiusFactor - safeZone);
        float requested = insideLateral - safeZone;

        float compressed;
        if (maxExtent < 0.1f)
        {
            compressed = 0f;
        }
        else
        {
            // Exponential falloff: asymptotically approaches maxExtent.
            compressed = maxExtent * (1f - Mathf.Exp(-requested / maxExtent));
        }

        return (safeZone + compressed) * insideSign;
    }

    private void SpawnTreesForChunk(int chunkIndex, GameObject chunkObject, Mesh visualMesh, int startSampleIndex, int endSampleIndex)
    {
        if (config == null || !config.spawnForestTrees || chunkObject == null || visualMesh == null)
        {
            return;
        }

        int treesPerChunk = Mathf.Max(0, config.forestTreesPerChunk);
        if (treesPerChunk <= 0)
        {
            return;
        }

        float safeRadius = Mathf.Max(0f, config.treeTrunkSafeRadius);
        int attempts = Mathf.Max(treesPerChunk, treesPerChunk * Mathf.Max(1, config.treeSpawnAttemptsMultiplier));

        List<ForestTriangle> forestTriangles = BuildForestTriangles(visualMesh);
        if (forestTriangles.Count == 0)
        {
            if (chunkIndex == 0)
            {
                Debug.LogWarning("[RoadStream] No eligible forest-floor triangles found for tree spawning in chunk 0.");
            }
            return;
        }

        var rng = new System.Random((config.seed * 73856093) ^ (chunkIndex * 19349663));
        var localTreePositions = new List<Vector2>(treesPerChunk);

        for (int attempt = 0; attempt < attempts && localTreePositions.Count < treesPerChunk; attempt++)
        {
            Vector3 spawnPoint;
            if (!TrySamplePointOnForestFloor(forestTriangles, rng, out spawnPoint))
            {
                break;
            }

            Vector2 spawn2D = new Vector2(spawnPoint.x, spawnPoint.z);
            if (safeRadius > 0.001f && (IsTreeTooClose(spawn2D, safeRadius) || IsTreeTooClose(localTreePositions, spawn2D, safeRadius)))
            {
                continue;
            }

            if (!IsPointFarEnoughFromDitch(spawnPoint, startSampleIndex, endSampleIndex))
            {
                continue;
            }

            GameObject prefab = SelectTreePrefab(rng, out bool isBirch);
            if (prefab == null)
            {
                break;
            }

            Quaternion yaw = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            Quaternion modelOffset = Quaternion.Euler(config.treeModelRotationOffsetEuler);
            GameObject instance = Instantiate(prefab, spawnPoint, yaw * modelOffset, chunkObject.transform);
            ResolveTreeFallbackMaterials(isBirch, out Material leafFallback, out Material barkFallback);
            ApplyTreeFallbackMaterialsIfNeeded(instance, leafFallback, barkFallback);
            AddTreeTrunkCollider(chunkObject.transform, spawnPoint, localTreePositions.Count);
            localTreePositions.Add(spawn2D);
        }

        if (localTreePositions.Count > 0)
        {
            chunkTreePositions[chunkIndex] = localTreePositions;
        }
    }

    private List<ForestTriangle> BuildForestTriangles(Mesh mesh)
    {
        var result = new List<ForestTriangle>(128);
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Color[] colors = mesh.colors;

        if (vertices == null || triangles == null || colors == null || colors.Length != vertices.Length)
        {
            return result;
        }

        float cumulativeArea = 0f;
        for (int i = 0; i <= triangles.Length - 3; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length)
            {
                continue;
            }

            float avgBlue = (colors[i0].b + colors[i1].b + colors[i2].b) / 3f;
            float avgRed = (colors[i0].r + colors[i1].r + colors[i2].r) / 3f;
            // Forest band blends with dirt near ditch edge, so allow mixed B while excluding asphalt-heavy tris.
            if (avgBlue < 0.15f || avgRed > 0.2f)
            {
                continue;
            }

            Vector3 a = vertices[i0];
            Vector3 b = vertices[i1];
            Vector3 c = vertices[i2];
            Vector3 normal = Vector3.Cross(b - a, c - a);
            float area = normal.magnitude * 0.5f;
            if (area < 0.001f)
            {
                continue;
            }

            if (Vector3.Dot(normal.normalized, Vector3.up) < 0.3f)
            {
                continue;
            }

            cumulativeArea += area;
            result.Add(new ForestTriangle
            {
                a = a,
                b = b,
                c = c,
                cumulativeArea = cumulativeArea
            });
        }

        return result;
    }

    private bool TrySamplePointOnForestFloor(List<ForestTriangle> forestTriangles, System.Random rng, out Vector3 point)
    {
        point = Vector3.zero;
        if (forestTriangles == null || forestTriangles.Count == 0)
        {
            return false;
        }

        float totalArea = forestTriangles[forestTriangles.Count - 1].cumulativeArea;
        if (totalArea <= 0f)
        {
            return false;
        }

        float targetArea = (float)rng.NextDouble() * totalArea;
        int triangleIndex = 0;
        while (triangleIndex < forestTriangles.Count - 1 && forestTriangles[triangleIndex].cumulativeArea < targetArea)
        {
            triangleIndex++;
        }

        ForestTriangle tri = forestTriangles[triangleIndex];
        float r1 = Mathf.Sqrt((float)rng.NextDouble());
        float r2 = (float)rng.NextDouble();
        float u = 1f - r1;
        float v = r1 * (1f - r2);
        float w = r1 * r2;
        point = tri.a * u + tri.b * v + tri.c * w;
        return true;
    }

    private bool IsTreeTooClose(Vector2 position, float safeRadius)
    {
        float safeRadiusSq = safeRadius * safeRadius;
        foreach (KeyValuePair<int, List<Vector2>> pair in chunkTreePositions)
        {
            List<Vector2> treePositions = pair.Value;
            for (int i = 0; i < treePositions.Count; i++)
            {
                if ((treePositions[i] - position).sqrMagnitude < safeRadiusSq)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsPointFarEnoughFromDitch(Vector3 point, int startSampleIndex, int endSampleIndex)
    {
        if (config == null || samples.Count == 0)
        {
            return true;
        }

        int start = Mathf.Clamp(startSampleIndex, 0, samples.Count - 1);
        int end = Mathf.Clamp(endSampleIndex, start, samples.Count - 1);

        int nearestIndex = start;
        float nearestDistSq = float.MaxValue;
        Vector2 point2D = new Vector2(point.x, point.z);
        for (int i = start; i <= end; i++)
        {
            Vector3 samplePos = samples[i].position;
            Vector2 sample2D = new Vector2(samplePos.x, samplePos.z);
            float distSq = (sample2D - point2D).sqrMagnitude;
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                nearestIndex = i;
            }
        }

        RoadSample nearestSample = samples[nearestIndex];
        Vector3 delta = point - nearestSample.position;
        float lateral = Mathf.Abs(Vector3.Dot(delta, nearestSample.right));
        float ditchOuter = config.roadWidth * 0.5f + Mathf.Max(0f, config.shoulderWidth) + Mathf.Max(0f, config.ditchWidth);
        float required = ditchOuter + Mathf.Max(0f, config.treeDitchClearance);
        return lateral >= required;
    }

    private static bool IsTreeTooClose(List<Vector2> treePositions, Vector2 position, float safeRadius)
    {
        float safeRadiusSq = safeRadius * safeRadius;
        for (int i = 0; i < treePositions.Count; i++)
        {
            if ((treePositions[i] - position).sqrMagnitude < safeRadiusSq)
            {
                return true;
            }
        }

        return false;
    }

    private void AddTreeTrunkCollider(Transform parent, Vector3 worldPosition, int index)
    {
        if (config == null || parent == null)
        {
            return;
        }

        float width = Mathf.Max(0.1f, config.treeColliderWidth);
        float height = Mathf.Max(0.5f, config.treeColliderHeight);

        GameObject colliderObject = new GameObject($"TreeCollider_{index:000}");
        colliderObject.transform.SetParent(parent, true);
        colliderObject.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);

        BoxCollider box = colliderObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, height * 0.5f, 0f);
        box.size = new Vector3(width, height, width);
    }

    private GameObject SelectTreePrefab(System.Random rng, out bool isBirch)
    {
        bool chooseBirch = rng.NextDouble() < Mathf.Clamp01(config.birchRatio);
        isBirch = chooseBirch;
        GameObject[] primary = chooseBirch ? birchTreePrefabs : pineTreePrefabs;
        GameObject[] secondary = chooseBirch ? pineTreePrefabs : birchTreePrefabs;

        GameObject prefab = ChooseRandomPrefab(primary, rng);
        if (prefab != null)
        {
            return prefab;
        }

        isBirch = !chooseBirch;
        return ChooseRandomPrefab(secondary, rng);
    }

    private static GameObject ChooseRandomPrefab(GameObject[] prefabs, System.Random rng)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return null;
        }

        int start = rng.Next(0, prefabs.Length);
        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[(start + i) % prefabs.Length];
            if (prefab != null)
            {
                return prefab;
            }
        }

        return null;
    }

    private void ResolveTreeFallbackMaterials(bool isBirch, out Material leafFallback, out Material barkFallback)
    {
        leafFallback = isBirch ? birchLeafFallbackMaterial : pineLeafFallbackMaterial;
        barkFallback = isBirch ? birchBarkFallbackMaterial : pineBarkFallbackMaterial;

#if UNITY_EDITOR
        if (leafFallback == null)
        {
            string leafPath = isBirch
                ? "Assets/Materials/Trees/BirchStylizedLeaf.mat"
                : "Assets/Materials/Trees/PineStylizedLeaf.mat";
            leafFallback = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(leafPath);
        }

        if (barkFallback == null)
        {
            string barkPath = isBirch
                ? "Assets/Materials/Trees/BirchBarkFallback.mat"
                : "Assets/Materials/Trees/PineBarkFallback.mat";
            barkFallback = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(barkPath);
        }

        if (isBirch)
        {
            if (leafFallback != null)
            {
                birchLeafFallbackMaterial = leafFallback;
            }

            if (barkFallback != null)
            {
                birchBarkFallbackMaterial = barkFallback;
            }
        }
        else
        {
            if (leafFallback != null)
            {
                pineLeafFallbackMaterial = leafFallback;
            }

            if (barkFallback != null)
            {
                pineBarkFallbackMaterial = barkFallback;
            }
        }
#endif
    }

    private static void ApplyTreeFallbackMaterialsIfNeeded(GameObject instance, Material leafFallbackMaterial, Material barkFallbackMaterial)
    {
        if (instance == null || leafFallbackMaterial == null)
        {
            return;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].sharedMaterials;
            Material bark = barkFallbackMaterial != null ? barkFallbackMaterial : leafFallbackMaterial;
            string rendererName = renderers[i].name ?? string.Empty;
            if (mats == null || mats.Length == 0)
            {
                renderers[i].sharedMaterial = IsBarkHint(rendererName, string.Empty, 0, 1)
                    ? bark
                    : leafFallbackMaterial;
                continue;
            }

            bool changed = false;
            for (int j = 0; j < mats.Length; j++)
            {
                Material target = IsBarkHint(rendererName, mats[j] != null ? mats[j].name : string.Empty, j, mats.Length)
                    ? bark
                    : leafFallbackMaterial;
                if (mats[j] != target)
                {
                    mats[j] = target;
                    changed = true;
                }
            }

            if (changed)
            {
                renderers[i].sharedMaterials = mats;
            }
        }
    }

    private static bool IsBarkHint(string rendererName, string materialName, int materialIndex, int materialCount)
    {
        string renderer = rendererName.ToLowerInvariant();
        string material = materialName.ToLowerInvariant();

        bool barkByName =
            renderer.Contains("bark") || renderer.Contains("trunk") || renderer.Contains("stem") || renderer.Contains("wood") ||
            material.Contains("bark") || material.Contains("trunk") || material.Contains("stem") || material.Contains("wood");
        if (barkByName)
        {
            return true;
        }

        bool leafByName =
            renderer.Contains("leaf") || renderer.Contains("leaves") || renderer.Contains("needle") || renderer.Contains("foliage") ||
            material.Contains("leaf") || material.Contains("leaves") || material.Contains("needle") || material.Contains("foliage");
        if (leafByName)
        {
            return false;
        }

        // Common tree import layout: slot 0 trunk, slot 1+ foliage.
        if (materialCount > 1 && materialIndex == 0)
        {
            return true;
        }

        return false;
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
            Vector3 horizontalForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            float slopeAngleDeg = 0f;
            if (config.enableHills)
            {
                float targetHeightNow = GetTargetElevation(s);
                float targetHeightPrev = GetTargetElevation(prev.s);
                float targetSlopeDeg = Mathf.Atan2(targetHeightNow - targetHeightPrev, sampleDistance) * Mathf.Rad2Deg;
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
                slopeAngleDeg = slopeAngleDeg
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
            slopeAngleDeg = 0f
        };
        samples.Add(first);
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

    private float GetTargetElevation(float s)
    {
        if (currentPieceType == PieceType.Designed &&
            currentDesignedPiece != null &&
            currentDesignedPiece.hasElevation &&
            s >= pieceStartS && s < pieceEndS)
        {
            float pieceLen = pieceEndS - pieceStartS;
            float t = pieceLen > 0f ? Mathf.Clamp01((s - pieceStartS) / pieceLen) : 0f;
            return transform.position.y + currentDesignedPiece.elevationCurve.Evaluate(t);
        }

        if (!config.enableHills)
        {
            return transform.position.y;
        }

        float smallWavelength = Mathf.Max(6f, config.smallBumpWavelength);
        float largeWavelength = Mathf.Max(60f, config.largeHillWavelength);
        float smallOmega = (Mathf.PI * 2f) / smallWavelength;
        float largeOmega = (Mathf.PI * 2f) / largeWavelength;
        float bumpPatchLength = Mathf.Max(20f, config.smallBumpPatchLength);

        float seedOffset = config.seed * 0.137f;
        float smallPhase = (s + seedOffset * 17f) * smallOmega;
        float largePhaseA = (s + seedOffset * 53f) * largeOmega;
        float largePhaseB = (s + seedOffset * 29f) * (largeOmega * 0.45f);

        float bumpMaskNoise = Mathf.PerlinNoise((s + seedOffset * 97f) / bumpPatchLength, 0.37f);
        float bumpThreshold = 1f - Mathf.Clamp01(config.smallBumpOccurrence);
        float bumpMask = Mathf.InverseLerp(bumpThreshold, 1f, bumpMaskNoise);
        bumpMask = bumpMask * bumpMask * (3f - 2f * bumpMask);

        float small = Mathf.Sin(smallPhase) * config.smallBumpAmplitude * bumpMask;
        float large = (Mathf.Sin(largePhaseA) * 0.7f + Mathf.Sin(largePhaseB) * 0.3f) * config.largeHillAmplitude;

        return transform.position.y + small + large;
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
            chunkTreePositions.Remove(key);
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
        chunkTreePositions.Clear();
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
