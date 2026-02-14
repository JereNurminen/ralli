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

    [Header("Runtime")]
    [SerializeField] private bool generateOnStart = true;

    private readonly List<RoadSample> samples = new List<RoadSample>(4096);
    private readonly Dictionary<int, ChunkData> chunks = new Dictionary<int, ChunkData>();
    private readonly List<ChunkLayout> chunkLayouts = new List<ChunkLayout>(256);

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
            float vCoord = sample.s * 0.1f;

            for (int j = 0; j < profileCount; j++)
            {
                ProfilePoint point = profile[j];
                float adjustedLateral = AdjustLateralForCurvature(point.lateral, sample.turnRateDegPerMeter);
                Vector3 top = sample.position + sample.right * adjustedLateral - sample.up * point.drop;
                Vector3 bottom = top - sample.up * thickness;
                float u = profileCount <= 1 ? 0f : j / (float)(profileCount - 1);

                int topIndex = rowBase + j;
                int bottomIndex = rowBase + profileCount + j;

                vertices[topIndex] = top;
                vertices[bottomIndex] = bottom;
                Vector3 topNormal = (sample.right * profileNormals[j].x + sample.up * profileNormals[j].y).normalized;
                normals[topIndex] = topNormal;
                normals[bottomIndex] = -topNormal;
                colors[topIndex] = point.color;
                colors[bottomIndex] = point.color;
                uv[topIndex] = new Vector2(u, vCoord);
                uv[bottomIndex] = new Vector2(u, vCoord);
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
        float maxExtent = Mathf.Max(0f, radius * 0.4f - safeZone);
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
