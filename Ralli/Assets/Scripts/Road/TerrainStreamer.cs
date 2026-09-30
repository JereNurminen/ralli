using System.Collections.Generic;
using UnityEngine;

// Streams world-space terrain tiles around the car. Ground height blends from the road's
// ditch lip next to the road (cut/fill) to the heightfield further out, so the terrain can
// never fold over itself the way a spline-extruded skirt does. Trees grow in a band along
// the road and get trunk colliders.
[RequireComponent(typeof(RoadStreamGenerator))]
public class TerrainStreamer : MonoBehaviour
{
    private class Tile
    {
        public GameObject gameObject;
        public Mesh mesh;
    }

    [Header("References")]
    [SerializeField] private RoadStreamGenerator road;
    [SerializeField] private Transform target;
    [SerializeField] private Material groundMaterial;

    [Header("Trees")]
    [SerializeField] private GameObject[] birchTreePrefabs;
    [SerializeField] private GameObject[] pineTreePrefabs;
    [SerializeField] private Material birchLeafFallbackMaterial;
    [SerializeField] private Material pineLeafFallbackMaterial;
    [SerializeField] private Material birchBarkFallbackMaterial;
    [SerializeField] private Material pineBarkFallbackMaterial;

    private readonly Dictionary<Vector2Int, Tile> tiles = new Dictionary<Vector2Int, Tile>();
    private readonly HashSet<Vector2Int> dirtyTiles = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> buildCandidates = new List<Vector2Int>();
    private readonly List<Vector2Int> tilesToRemove = new List<Vector2Int>();
    private int knownGeneration = -1;
    private int checkedSampleCount;

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
            checkedSampleCount = 0;
        }

        MarkTilesTouchedByNewRoad(config);

        Vector3 center = GetStreamCenter(config);
        float tileSize = Mathf.Max(8f, config.terrainTileSize);
        float radius = Mathf.Max(tileSize, config.terrainRadius);

        BuildNearestMissingTiles(center, tileSize, radius, Mathf.Max(1, config.terrainTilesPerFrame));
        RemoveFarTiles(center, tileSize, radius + tileSize);
    }

    private Vector3 GetStreamCenter(RoadGenerationConfig config)
    {
        Vector3 origin = target != null ? target.position : transform.position;
        return origin + road.GetTargetBearingDirection() * config.terrainBearingBias;
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
        float reach = GetInfluenceReach(config);

        for (int i = checkedSampleCount; i < sampleCount; i++)
        {
            if (tiles.Count == 0)
            {
                break;
            }

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

    private float GetInfluenceReach(RoadGenerationConfig config)
    {
        float ground = road.CorridorHalfWidth + config.roadFlatRing + config.roadBlendWidth;
        float trees = road.CorridorHalfWidth + config.treeDitchClearance + config.treeBandWidth;
        return Mathf.Max(ground, trees);
    }

    private void BuildNearestMissingTiles(Vector3 center, float tileSize, float radius, int maxBuilds)
    {
        buildCandidates.Clear();
        int minX = Mathf.FloorToInt((center.x - radius) / tileSize);
        int maxX = Mathf.FloorToInt((center.x + radius) / tileSize);
        int minZ = Mathf.FloorToInt((center.z - radius) / tileSize);
        int maxZ = Mathf.FloorToInt((center.z + radius) / tileSize);

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector2Int coord = new Vector2Int(x, z);
                if (GetTileDistance(coord, center, tileSize) > radius)
                {
                    continue;
                }

                if (!tiles.ContainsKey(coord) || dirtyTiles.Contains(coord))
                {
                    buildCandidates.Add(coord);
                }
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

    private void RemoveFarTiles(Vector3 center, float tileSize, float keepRadius)
    {
        tilesToRemove.Clear();
        foreach (KeyValuePair<Vector2Int, Tile> pair in tiles)
        {
            if (GetTileDistance(pair.Key, center, tileSize) > keepRadius)
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
        Vector3 origin = new Vector3(coord.x * tileSize, 0f, coord.y * tileSize);
        int n = resolution + 1;

        // Heights include a one-vertex border so normals match across tile edges.
        float[,] heights = new float[n + 2, n + 2];
        for (int i = 0; i < n + 2; i++)
        {
            for (int j = 0; j < n + 2; j++)
            {
                heights[i, j] = SampleGround(origin.x + (i - 1) * step, origin.z + (j - 1) * step);
            }
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

        int[] triangles = new int[resolution * resolution * 6];
        int t = 0;
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                int a = j * n + i;
                int b = (j + 1) * n + i;
                int c = (j + 1) * n + i + 1;
                int d = j * n + i + 1;
                triangles[t++] = a;
                triangles[t++] = b;
                triangles[t++] = c;
                triangles[t++] = a;
                triangles[t++] = c;
                triangles[t++] = d;
            }
        }

        Mesh mesh = new Mesh
        {
            name = $"Terrain_{coord.x}_{coord.y}",
            vertices = vertices,
            normals = normals,
            colors = colors,
            triangles = triangles
        };
        mesh.RecalculateBounds();

        GameObject tileObject = new GameObject($"TerrainTile_{coord.x}_{coord.y}");
        tileObject.transform.SetParent(transform, false);
        tileObject.transform.SetPositionAndRotation(origin, Quaternion.identity);
        tileObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        tileObject.AddComponent<MeshRenderer>().sharedMaterial = groundMaterial;
        tileObject.AddComponent<MeshCollider>().sharedMesh = mesh;

        SpawnTrees(tileObject.transform, origin, tileSize);
        return new Tile { gameObject = tileObject, mesh = mesh };
    }

    // Road-aware ground height: tucked under the road corridor, flat at lip height right
    // outside it, then blending to the heightfield (this blend is the cut/fill face).
    private float SampleGround(float x, float z)
    {
        RoadGenerationConfig config = road.Config;
        float terrainHeight = road.HeightField.GetHeight(x, z);
        float corridor = road.CorridorHalfWidth;
        float flatRing = Mathf.Max(0f, config.roadFlatRing);
        float blend = Mathf.Max(0.01f, config.roadBlendWidth);

        if (!road.TryGetRoadProximity(x, z, corridor + flatRing + blend, out float distance, out float lipHeight))
        {
            return terrainHeight;
        }

        if (distance <= corridor)
        {
            return lipHeight - Mathf.Max(0f, config.corridorTuckDepth);
        }

        float t = Mathf.Clamp01((distance - corridor - flatRing) / blend);
        t = t * t * (3f - 2f * t);
        return Mathf.Lerp(lipHeight, terrainHeight, t);
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
                if (TerrainHeightField.Hash01(config.seed, cx, cz, 1) >= config.treeDensity)
                {
                    continue;
                }

                float px = (cx + Mathf.Lerp(0.15f, 0.85f, TerrainHeightField.Hash01(config.seed, cx, cz, 2))) * cell;
                float pz = (cz + Mathf.Lerp(0.15f, 0.85f, TerrainHeightField.Hash01(config.seed, cx, cz, 3))) * cell;
                if (!road.TryGetRoadProximity(px, pz, outer, out float distance, out _) || distance < inner)
                {
                    continue;
                }

                var rng = new System.Random((int)(TerrainHeightField.Hash01(config.seed, cx, cz, 4) * int.MaxValue));
                GameObject prefab = SelectTreePrefab(config, rng, out bool isBirch);
                if (prefab == null)
                {
                    return;
                }

                Vector3 position = new Vector3(px, SampleGround(px, pz), pz);
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

        if (tile.mesh != null)
        {
            Destroy(tile.mesh);
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
