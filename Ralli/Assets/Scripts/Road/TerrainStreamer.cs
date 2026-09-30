using System.Collections.Generic;
using UnityEngine;

// Streams world-space terrain tiles in a band along the road. Ground height blends from the
// road's ditch lip (cut/fill) to the heightfield further out, so it can never fold over itself
// the way a spline-extruded skirt does. The band ends in a solid wall (distance-to-road contour,
// traced per tile). Trees grow in a strip along the road and get trunk colliders. Gas station lots
// count as part of the road corridor: flat ground on them, and the tree strip and wall continue
// behind them. Past a dead end of the road the forest floor carries on flush and trees close in. Tree placement is random per road build (not tied to the road seed).
[RequireComponent(typeof(RoadStreamGenerator))]
public class TerrainStreamer : MonoBehaviour
{
    private class Tile
    {
        public GameObject gameObject;
        public Mesh groundMesh;
        public Mesh wallMesh;
    }

    [Header("References")]
    [SerializeField] private RoadStreamGenerator road;
    [SerializeField] private Transform target;
    [SerializeField] private Material groundMaterial;

    [Header("Band Wall")]
    [Tooltip("Material for the wall at the terrain band edge. Empty = unlit solid Wall Color.")]
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Color wallColor = new Color(0.16f, 0.22f, 0.16f, 1f);

    [Header("Trees")]
    [SerializeField] private GameObject[] birchTreePrefabs;
    [SerializeField] private GameObject[] pineTreePrefabs;
    [SerializeField] private Material birchLeafFallbackMaterial;
    [SerializeField] private Material pineLeafFallbackMaterial;
    [SerializeField] private Material birchBarkFallbackMaterial;
    [SerializeField] private Material pineBarkFallbackMaterial;

    private readonly Dictionary<Vector2Int, Tile> tiles = new Dictionary<Vector2Int, Tile>();
    private readonly HashSet<Vector2Int> wantedTiles = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> dirtyTiles = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> buildCandidates = new List<Vector2Int>();
    private readonly List<Vector2Int> tilesToRemove = new List<Vector2Int>();
    private readonly List<int> nearbySamples = new List<int>();
    private readonly List<Vector2> nearbySamplePositions = new List<Vector2>();
    private Material runtimeWallMaterial;
    private int knownGeneration = -1;
    private int checkedSampleCount;
    private int wantedSampleCount = -1;
    private Vector3 wantedCenter;
    private int treeSeed;

    private void Start()
    {
        if (road == null)
        {
            road = GetComponent<RoadStreamGenerator>();
        }

        if (target == null)
        {
            CarController car = FindFirstObjectByType<CarController>();
            if (car != null)
            {
                target = car.transform;
            }
        }
    }

    private void Update()
    {
        if (road == null || !road.IsReady)
        {
            return;
        }

        RoadGenerationConfig config = road.Config;
        if (road.Generation != knownGeneration)
        {
            ClearTiles();
            knownGeneration = road.Generation;
            treeSeed = Random.Range(1, int.MaxValue);
            checkedSampleCount = 0;
            wantedSampleCount = -1;
        }

        MarkTilesTouchedByNewRoad(config);

        Vector3 center = GetStreamCenter(config);
        float tileSize = Mathf.Max(8f, config.terrainTileSize);
        RefreshWantedTiles(config, center, tileSize);
        BuildNearestMissingTiles(center, tileSize, Mathf.Max(1, config.terrainTilesPerFrame));
        RemoveUnwantedTiles();
    }

    private Vector3 GetStreamCenter(RoadGenerationConfig config)
    {
        Vector3 origin = target != null ? target.position : transform.position;
        return origin + road.GetTargetBearingDirection() * config.terrainBearingBias;
    }

    // The band (and its wall) ends just past the outer edge of tree coverage.
    private float GetBandHalfWidth(RoadGenerationConfig config)
    {
        float treeOuter = road.CorridorHalfWidth + Mathf.Max(0f, config.treeDitchClearance) + Mathf.Max(0f, config.treeBandWidth);
        return treeOuter + Mathf.Max(0f, config.terrainWallMargin);
    }

    // New road samples (rare: the road looping back later) invalidate tiles they now influence.
    private void MarkTilesTouchedByNewRoad(RoadGenerationConfig config)
    {
        int sampleCount = road.SampleCount;
        if (checkedSampleCount >= sampleCount)
        {
            return;
        }

        float tileSize = Mathf.Max(8f, config.terrainTileSize);
        float reach = GetBandHalfWidth(config) + tileSize / Mathf.Max(2, config.terrainTileResolution) * 2f;

        for (int i = checkedSampleCount; i < sampleCount && tiles.Count > 0; i++)
        {
            Vector3 p = road.GetSamplePosition(i);
            int minX = Mathf.FloorToInt((p.x - reach) / tileSize);
            int maxX = Mathf.FloorToInt((p.x + reach) / tileSize);
            int minZ = Mathf.FloorToInt((p.z - reach) / tileSize);
            int maxZ = Mathf.FloorToInt((p.z + reach) / tileSize);
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector2Int coord = new Vector2Int(x, z);
                    if (tiles.ContainsKey(coord))
                    {
                        dirtyTiles.Add(coord);
                    }
                }
            }
        }

        checkedSampleCount = sampleCount;
    }

    // Tiles within the band around road samples that are within terrainRadius of the center.
    // Recomputed only after moving half a tile or when the road grows.
    private void RefreshWantedTiles(RoadGenerationConfig config, Vector3 center, float tileSize)
    {
        Vector3 moved = center - wantedCenter;
        moved.y = 0f;
        if (wantedSampleCount == road.SampleCount && moved.sqrMagnitude < tileSize * tileSize * 0.25f)
        {
            return;
        }

        wantedCenter = center;
        wantedSampleCount = road.SampleCount;
        wantedTiles.Clear();

        float radius = Mathf.Max(tileSize, config.terrainRadius);
        float band = GetBandHalfWidth(config);
        float tileHalfDiagonal = tileSize * 0.7072f;
        int stride = Mathf.Max(1, Mathf.RoundToInt(tileSize * 0.5f / Mathf.Max(0.01f, road.SampleSpacing)));
        Vector2 center2 = new Vector2(center.x, center.z);

        for (int i = 0; i < road.SampleCount; i += stride)
        {
            Vector3 p = road.GetSamplePosition(i);
            Vector2 p2 = new Vector2(p.x, p.z);
            if (Vector2.Distance(p2, center2) > radius)
            {
                continue;
            }

            float reach = band + tileHalfDiagonal + tileSize * 0.5f;
            int minX = Mathf.FloorToInt((p.x - reach) / tileSize);
            int maxX = Mathf.FloorToInt((p.x + reach) / tileSize);
            int minZ = Mathf.FloorToInt((p.z - reach) / tileSize);
            int maxZ = Mathf.FloorToInt((p.z + reach) / tileSize);
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector2 tileCenter = new Vector2((x + 0.5f) * tileSize, (z + 0.5f) * tileSize);
                    if (Vector2.Distance(tileCenter, p2) <= reach)
                    {
                        wantedTiles.Add(new Vector2Int(x, z));
                    }
                }
            }
        }
    }

    private void BuildNearestMissingTiles(Vector3 center, float tileSize, int maxBuilds)
    {
        buildCandidates.Clear();
        foreach (Vector2Int coord in wantedTiles)
        {
            if (!tiles.ContainsKey(coord) || dirtyTiles.Contains(coord))
            {
                buildCandidates.Add(coord);
            }
        }

        if (buildCandidates.Count == 0)
        {
            return;
        }

        buildCandidates.Sort((a, b) => GetTileDistance(a, center, tileSize).CompareTo(GetTileDistance(b, center, tileSize)));
        int count = Mathf.Min(maxBuilds, buildCandidates.Count);
        for (int i = 0; i < count; i++)
        {
            Vector2Int coord = buildCandidates[i];
            DestroyTile(coord);
            dirtyTiles.Remove(coord);
            tiles[coord] = BuildTile(coord, tileSize);
        }
    }

    private void RemoveUnwantedTiles()
    {
        tilesToRemove.Clear();
        foreach (KeyValuePair<Vector2Int, Tile> pair in tiles)
        {
            if (!wantedTiles.Contains(pair.Key))
            {
                tilesToRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < tilesToRemove.Count; i++)
        {
            DestroyTile(tilesToRemove[i]);
            dirtyTiles.Remove(tilesToRemove[i]);
        }
    }

    private static float GetTileDistance(Vector2Int coord, Vector3 center, float tileSize)
    {
        float cx = (coord.x + 0.5f) * tileSize;
        float cz = (coord.y + 0.5f) * tileSize;
        return Vector2.Distance(new Vector2(cx, cz), new Vector2(center.x, center.z));
    }

    private Tile BuildTile(Vector2Int coord, float tileSize)
    {
        RoadGenerationConfig config = road.Config;
        int resolution = Mathf.Clamp(config.terrainTileResolution, 2, 128);
        float step = tileSize / resolution;
        float band = GetBandHalfWidth(config);
        Vector3 origin = new Vector3(coord.x * tileSize, 0f, coord.y * tileSize);
        int n = resolution + 1;

        CollectNearbySamples(origin, tileSize, band + step * 3f);

        // Heights and road distances include a one-vertex border so normals match across tiles.
        float[,] heights = new float[n + 2, n + 2];
        float[,] distances = new float[n + 2, n + 2];
        for (int i = 0; i < n + 2; i++)
        {
            for (int j = 0; j < n + 2; j++)
            {
                float x = origin.x + (i - 1) * step;
                float z = origin.z + (j - 1) * step;
                heights[i, j] = SampleGround(x, z, out distances[i, j], out _);
            }
        }

        GameObject tileObject = new GameObject($"TerrainTile_{coord.x}_{coord.y}");
        tileObject.transform.SetParent(transform, false);
        tileObject.transform.SetPositionAndRotation(origin, Quaternion.identity);

        Tile tile = new Tile { gameObject = tileObject };
        tile.groundMesh = BuildGroundMesh(coord, resolution, step, heights, distances, band);
        if (tile.groundMesh != null)
        {
            tileObject.AddComponent<MeshFilter>().sharedMesh = tile.groundMesh;
            tileObject.AddComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            tileObject.AddComponent<MeshCollider>().sharedMesh = tile.groundMesh;
        }

        tile.wallMesh = BuildWallMesh(coord, resolution, step, heights, distances, band);
        if (tile.wallMesh != null)
        {
            GameObject wallObject = new GameObject("BandWall");
            wallObject.transform.SetParent(tileObject.transform, false);
            wallObject.AddComponent<MeshFilter>().sharedMesh = tile.wallMesh;
            wallObject.AddComponent<MeshRenderer>().sharedMaterial = ResolveWallMaterial();
            wallObject.AddComponent<MeshCollider>().sharedMesh = tile.wallMesh;
        }

        SpawnTrees(tileObject.transform, origin, tileSize);
        return tile;
    }

    // Ground quads are kept if any corner is inside the band; the wall hides the ragged edge.
    private Mesh BuildGroundMesh(Vector2Int coord, int resolution, float step, float[,] heights, float[,] distances, float band)
    {
        int n = resolution + 1;
        var triangles = new List<int>(resolution * resolution * 6);
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                bool inside = distances[i + 1, j + 1] <= band || distances[i + 2, j + 1] <= band
                              || distances[i + 1, j + 2] <= band || distances[i + 2, j + 2] <= band;
                if (!inside)
                {
                    continue;
                }

                int a = j * n + i;
                int b = (j + 1) * n + i;
                int c = (j + 1) * n + i + 1;
                int d = j * n + i + 1;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        if (triangles.Count == 0)
        {
            return null;
        }

        Vector3[] vertices = new Vector3[n * n];
        Vector3[] normals = new Vector3[n * n];
        Color[] colors = new Color[n * n];
        Color forest = new Color(0f, 1f, 1f, 0f);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                int index = j * n + i;
                vertices[index] = new Vector3(i * step, heights[i + 1, j + 1], j * step);
                float dx = heights[i + 2, j + 1] - heights[i, j + 1];
                float dz = heights[i + 1, j + 2] - heights[i + 1, j];
                normals[index] = new Vector3(-dx, 2f * step, -dz).normalized;
                colors[index] = forest;
            }
        }

        Mesh mesh = new Mesh
        {
            name = $"Terrain_{coord.x}_{coord.y}",
            vertices = vertices,
            normals = normals,
            colors = colors
        };
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Marching squares on (distance - band): each grid cell the band edge crosses gets one
    // vertical, double-sided wall quad. Because it follows a distance contour it cannot fold.
    private Mesh BuildWallMesh(Vector2Int coord, int resolution, float step, float[,] heights, float[,] distances, float band)
    {
        RoadGenerationConfig config = road.Config;
        float wallHeight = Mathf.Max(0.5f, config.terrainWallHeight);
        float sink = Mathf.Max(0f, config.terrainWallSink);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var crossings = new List<Vector3>(4);

        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                crossings.Clear();
                // Corners in loop order: (i,j) → (i+1,j) → (i+1,j+1) → (i,j+1).
                AddCrossing(crossings, i, j, i + 1, j, step, heights, distances, band);
                AddCrossing(crossings, i + 1, j, i + 1, j + 1, step, heights, distances, band);
                AddCrossing(crossings, i + 1, j + 1, i, j + 1, step, heights, distances, band);
                AddCrossing(crossings, i, j + 1, i, j, step, heights, distances, band);

                for (int k = 0; k + 1 < crossings.Count; k += 2)
                {
                    AddWallQuad(vertices, triangles, crossings[k], crossings[k + 1], wallHeight, sink);
                }
            }
        }

        if (triangles.Count == 0)
        {
            return null;
        }

        Mesh mesh = new Mesh { name = $"TerrainWall_{coord.x}_{coord.y}" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddCrossing(List<Vector3> crossings, int ai, int aj, int bi, int bj, float step, float[,] heights, float[,] distances, float band)
    {
        float fa = distances[ai + 1, aj + 1] - band;
        float fb = distances[bi + 1, bj + 1] - band;
        if ((fa <= 0f) == (fb <= 0f))
        {
            return;
        }

        float t = fa / (fa - fb);
        float x = Mathf.Lerp(ai, bi, t) * step;
        float z = Mathf.Lerp(aj, bj, t) * step;
        float y = Mathf.Lerp(heights[ai + 1, aj + 1], heights[bi + 1, bj + 1], t);
        crossings.Add(new Vector3(x, y, z));
    }

    private static void AddWallQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, float wallHeight, float sink)
    {
        int start = vertices.Count;
        vertices.Add(new Vector3(a.x, a.y - sink, a.z));
        vertices.Add(new Vector3(b.x, b.y - sink, b.z));
        vertices.Add(new Vector3(b.x, b.y + wallHeight, b.z));
        vertices.Add(new Vector3(a.x, a.y + wallHeight, a.z));

        // Both windings so the wall shows from either side.
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start);
        triangles.Add(start + 3);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    private Material ResolveWallMaterial()
    {
        if (wallMaterial != null)
        {
            return wallMaterial;
        }

        if (runtimeWallMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            runtimeWallMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"))
            {
                name = "Runtime_BandWall",
                hideFlags = HideFlags.DontSave
            };
            if (runtimeWallMaterial.HasProperty("_BaseColor"))
            {
                runtimeWallMaterial.SetColor("_BaseColor", wallColor);
            }
            runtimeWallMaterial.color = wallColor;
        }

        return runtimeWallMaterial;
    }

    // Grabs road samples near a tile once, so per-vertex nearest-road lookups are a flat loop.
    private void CollectNearbySamples(Vector3 origin, float tileSize, float reach)
    {
        nearbySamples.Clear();
        nearbySamplePositions.Clear();
        road.CollectSampleIndices(origin.x - reach, origin.z - reach, origin.x + tileSize + reach, origin.z + tileSize + reach, nearbySamples);
        for (int i = 0; i < nearbySamples.Count; i++)
        {
            Vector3 p = road.GetSamplePosition(nearbySamples[i]);
            nearbySamplePositions.Add(new Vector2(p.x, p.z));
        }
    }

    private int FindNearestSample(float x, float z, out float distance)
    {
        int best = -1;
        float bestSq = float.MaxValue;
        for (int i = 0; i < nearbySamplePositions.Count; i++)
        {
            Vector2 p = nearbySamplePositions[i];
            float sq = (p.x - x) * (p.x - x) + (p.y - z) * (p.y - z);
            if (sq < bestSq)
            {
                bestSq = sq;
                best = i;
            }
        }

        distance = best >= 0 ? Mathf.Sqrt(bestSq) : float.MaxValue;
        return best >= 0 ? nearbySamples[best] : -1;
    }

    // Road-aware ground height: tucked under the road corridor, flat at lip height right
    // outside it, then blending to the heightfield (this blend is the cut/fill face). Station lots
    // override it with their flat pad. roadDistance is the distance to the corridor (a station lot
    // counts as corridor), used for the band edge. treeDistance is how far trees must keep: the
    // same, except past a dead end, where it grows with the distance beyond the end.
    private float SampleGround(float x, float z, out float roadDistance, out float treeDistance)
    {
        RoadGenerationConfig config = road.Config;
        float terrainHeight = road.HeightField.GetHeight(x, z);
        float corridor = road.CorridorHalfWidth;
        float flatRing = Mathf.Max(0f, config.roadFlatRing);
        float blend = Mathf.Max(0.01f, config.roadBlendWidth);

        int sampleIndex = FindNearestSample(x, z, out roadDistance);
        treeDistance = roadDistance;
        float height = terrainHeight;
        if (sampleIndex >= 0 && roadDistance <= corridor + flatRing + blend)
        {
            float lipHeight = road.GetLipHeight(sampleIndex, x, z);
            float beyondEnd = GetDistanceBeyondRoadEnd(sampleIndex, x, z);
            if (beyondEnd > 0f)
            {
                // No road here: forest floor level with the road end, blending out to the terrain.
                treeDistance = Mathf.Max(roadDistance, road.CorridorHalfWidth + Mathf.Max(0f, config.treeDitchClearance) + beyondEnd - config.roadEndTreeGap);
                float tEnd = Mathf.Clamp01((roadDistance - flatRing) / blend);
                height = Mathf.Lerp(lipHeight, terrainHeight, tEnd * tEnd * (3f - 2f * tEnd));
            }
            else if (roadDistance <= corridor)
            {
                return lipHeight - Mathf.Max(0f, config.corridorTuckDepth);
            }
            else
            {
                float t = Mathf.Clamp01((roadDistance - corridor - flatRing) / blend);
                t = t * t * (3f - 2f * t);
                height = Mathf.Lerp(lipHeight, terrainHeight, t);
            }
        }

        float lotOutside = GetStationLotOutside(x, z, out float lotHeight);
        if (lotOutside < float.MaxValue)
        {
            if (roadDistance > corridor)
            {
                float t = Mathf.Clamp01(lotOutside / Mathf.Max(0.01f, config.stationLotTerrainBlend));
                height = Mathf.Lerp(lotHeight, height, t * t * (3f - 2f * t));
            }

            float lotDistance = corridor + lotOutside;
            roadDistance = Mathf.Min(roadDistance, lotDistance);
            treeDistance = Mathf.Min(treeDistance, lotDistance);
        }

        return height;
    }

    // How far (x, z) lies past a dead end of the road (0 when not past one).
    private float GetDistanceBeyondRoadEnd(int sampleIndex, float x, float z)
    {
        if (!road.IsRoadEndSample(sampleIndex))
        {
            return 0f;
        }

        Vector3 end = road.GetSamplePosition(sampleIndex);
        Vector3 outward = road.GetSampleTangent(sampleIndex) * (sampleIndex == 0 ? -1f : 1f);
        Vector2 outwardFlat = new Vector2(outward.x, outward.z).normalized;
        return Mathf.Max(0f, (x - end.x) * outwardFlat.x + (z - end.z) * outwardFlat.y);
    }

    // Distance from (x, z) to the nearest station lot, which reaches from the road centerline to
    // the back of the yard, and that lot's ground height. float.MaxValue when there are no lots.
    private float GetStationLotOutside(float x, float z, out float lotHeight)
    {
        lotHeight = 0f;
        float best = float.MaxValue;
        float toCenterline = road.Config.roadWidth * 0.5f + Mathf.Max(0f, road.Config.shoulderWidth);
        IReadOnlyList<RoadStreamGenerator.StationLot> lots = road.GetStationLots();
        for (int i = 0; i < lots.Count; i++)
        {
            RoadStreamGenerator.StationLot lot = lots[i];
            Vector3 offset = new Vector3(x - lot.origin.x, 0f, z - lot.origin.z);
            float across = Vector3.Dot(offset, lot.right);
            float outsideAcross = Mathf.Max(0f, across - lot.depth) + Mathf.Max(0f, -across - toCenterline);
            float outsideAlong = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(offset, lot.forward)) - lot.length * 0.5f);
            float outside = Mathf.Sqrt(outsideAcross * outsideAcross + outsideAlong * outsideAlong);
            if (outside < best)
            {
                best = outside;
                lotHeight = lot.origin.y + road.Config.forestFloorYOffset;
            }
        }

        return best;
    }

    private void SpawnTrees(Transform parent, Vector3 origin, float tileSize)
    {
        RoadGenerationConfig config = road.Config;
        if (!config.spawnForestTrees)
        {
            return;
        }

        float cell = Mathf.Max(1f, config.treeCellSize);
        float inner = road.CorridorHalfWidth + Mathf.Max(0f, config.treeDitchClearance);
        float outer = inner + Mathf.Max(0f, config.treeBandWidth);
        Quaternion modelOffset = Quaternion.Euler(config.treeModelRotationOffsetEuler);

        // A cell belongs to the tile that contains its min corner, so tiles never double-spawn.
        int minX = Mathf.CeilToInt(origin.x / cell);
        int maxX = Mathf.CeilToInt((origin.x + tileSize) / cell) - 1;
        int minZ = Mathf.CeilToInt(origin.z / cell);
        int maxZ = Mathf.CeilToInt((origin.z + tileSize) / cell) - 1;
        int treeIndex = 0;

        for (int cx = minX; cx <= maxX; cx++)
        {
            for (int cz = minZ; cz <= maxZ; cz++)
            {
                if (TerrainHeightField.Hash01(treeSeed, cx, cz, 1) >= config.treeDensity)
                {
                    continue;
                }

                float px = (cx + Mathf.Lerp(0.15f, 0.85f, TerrainHeightField.Hash01(treeSeed, cx, cz, 2))) * cell;
                float pz = (cz + Mathf.Lerp(0.15f, 0.85f, TerrainHeightField.Hash01(treeSeed, cx, cz, 3))) * cell;
                float groundHeight = SampleGround(px, pz, out _, out float distance);
                if (distance < inner || distance > outer)
                {
                    continue;
                }

                var rng = new System.Random((int)(TerrainHeightField.Hash01(treeSeed, cx, cz, 4) * int.MaxValue));
                GameObject prefab = SelectTreePrefab(config, rng, out bool isBirch);
                if (prefab == null)
                {
                    return;
                }

                Vector3 position = new Vector3(px, groundHeight, pz);
                Quaternion yaw = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                GameObject instance = Instantiate(prefab, position, yaw * modelOffset, parent);
                ResolveTreeFallbackMaterials(isBirch, out Material leafFallback, out Material barkFallback);
                ApplyTreeFallbackMaterialsIfNeeded(instance, leafFallback, barkFallback);
                AddTreeTrunkCollider(config, parent, position, treeIndex++);
            }
        }
    }

    private static void AddTreeTrunkCollider(RoadGenerationConfig config, Transform parent, Vector3 worldPosition, int index)
    {
        float height = Mathf.Max(0.5f, config.treeColliderHeight);
        GameObject colliderObject = new GameObject($"TreeCollider_{index:000}");
        colliderObject.transform.SetParent(parent, true);
        colliderObject.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);

        CapsuleCollider capsule = colliderObject.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.radius = Mathf.Max(0.05f, config.treeColliderWidth * 0.5f);
        capsule.height = height;
        capsule.center = new Vector3(0f, height * 0.5f, 0f);
    }

    private void DestroyTile(Vector2Int coord)
    {
        if (!tiles.TryGetValue(coord, out Tile tile))
        {
            return;
        }

        if (tile.gameObject != null)
        {
            Destroy(tile.gameObject);
        }

        if (tile.groundMesh != null)
        {
            Destroy(tile.groundMesh);
        }

        if (tile.wallMesh != null)
        {
            Destroy(tile.wallMesh);
        }

        tiles.Remove(coord);
    }

    private void ClearTiles()
    {
        tilesToRemove.Clear();
        tilesToRemove.AddRange(tiles.Keys);
        for (int i = 0; i < tilesToRemove.Count; i++)
        {
            DestroyTile(tilesToRemove[i]);
        }

        dirtyTiles.Clear();
        wantedTiles.Clear();
    }

    private GameObject SelectTreePrefab(RoadGenerationConfig config, System.Random rng, out bool isBirch)
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
        return materialCount > 1 && materialIndex == 0;
    }
}
