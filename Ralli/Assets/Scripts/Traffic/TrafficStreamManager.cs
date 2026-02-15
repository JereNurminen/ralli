using System.Collections.Generic;
using UnityEngine;

public class TrafficStreamManager : MonoBehaviour
{
    [SerializeField] private RoadStreamGenerator roadStream;
    [SerializeField] private TrafficConfig config;
    [SerializeField] private Transform vehicleRoot;

    private readonly Dictionary<int, List<TrafficVehicle>> chunkVehicles = new Dictionary<int, List<TrafficVehicle>>();
    private readonly HashSet<int> spawnedChunks = new HashSet<int>();
    private MaterialPropertyBlock propertyBlock;
    private Material trafficMaterial;

    private void Start()
    {
        if (roadStream == null)
        {
            roadStream = FindFirstObjectByType<RoadStreamGenerator>();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (vehicleRoot == null)
        {
            GameObject root = new GameObject("TrafficVehicles");
            root.transform.SetParent(transform, false);
            vehicleRoot = root.transform;
        }

        EnsureMaterial();
    }

    private void Update()
    {
        if (config == null || !config.enableTraffic || roadStream == null)
        {
            return;
        }

        if (!roadStream.TryGetActiveChunkRange(out int minChunk, out int maxChunk))
        {
            return;
        }

        int spawnMax = maxChunk + Mathf.Max(0, config.spawnAheadChunks);
        for (int chunkIndex = minChunk; chunkIndex <= spawnMax; chunkIndex++)
        {
            if (spawnedChunks.Contains(chunkIndex))
            {
                continue;
            }

            SpawnChunkTraffic(chunkIndex);
        }

        CullOutside(minChunk - 1, spawnMax + 1);
    }

    private void FixedUpdate()
    {
        if (chunkVehicles.Count == 0)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        foreach (KeyValuePair<int, List<TrafficVehicle>> pair in chunkVehicles)
        {
            List<TrafficVehicle> vehicles = pair.Value;
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i] != null)
                {
                    vehicles[i].Tick(dt);
                }
            }
        }
    }

    private void SpawnChunkTraffic(int chunkIndex)
    {
        if (!roadStream.TryGetChunkRangeS(chunkIndex, out float startS, out float endS))
        {
            spawnedChunks.Add(chunkIndex);
            return;
        }

        float chunkLength = Mathf.Max(1f, endS - startS);
        float perChunk = chunkLength * 0.001f * Mathf.Max(0f, config.vehiclesPerKilometer);
        int count = Mathf.Clamp(Mathf.RoundToInt(perChunk), 0, Mathf.Max(0, config.maxVehiclesPerChunk));
        if (count <= 0)
        {
            spawnedChunks.Add(chunkIndex);
            return;
        }

        System.Random rng = new System.Random((roadStream.GetSeed() * 83492791) ^ (chunkIndex * 19349663));
        var created = new List<TrafficVehicle>(count);
        var lanePositions = new Dictionary<int, List<float>>
        {
            { -1, new List<float>(count) },
            { 1, new List<float>(count) }
        };

        int attempts = count * 8;
        while (created.Count < count && attempts-- > 0)
        {
            float t = (float)rng.NextDouble();
            float s = Mathf.Lerp(startS + 3f, endS - 3f, t);
            bool sameDirection = rng.NextDouble() < Mathf.Clamp01(config.sameDirectionLaneChance);
            int laneSign = sameDirection ? 1 : -1;
            int directionSign = sameDirection ? 1 : -1;

            if (!IsLaneSpacingValid(lanePositions[laneSign], s, Mathf.Max(2f, config.sameLaneMinSpacing)))
            {
                continue;
            }

            float speedKph = Mathf.Max(10f, config.trafficSpeedKph + ((float)rng.NextDouble() * 2f - 1f) * Mathf.Max(0f, config.speedVarianceKph));
            Color bodyColor = GenerateTrafficColor(rng);
            TrafficVehicle vehicle = CreateBoxVehicle($"Traffic_{chunkIndex}_{created.Count:00}", bodyColor);
            vehicle.Initialize(roadStream, config, s, laneSign, directionSign, speedKph);

            lanePositions[laneSign].Add(s);
            created.Add(vehicle);
        }

        if (created.Count > 0)
        {
            chunkVehicles[chunkIndex] = created;
        }

        spawnedChunks.Add(chunkIndex);
    }

    private TrafficVehicle CreateBoxVehicle(string name, Color bodyColor)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(vehicleRoot, true);

        Vector3 size = config != null ? config.vehicleBoxSize : new Vector3(1.8f, 1.4f, 4.2f);
        go.transform.localScale = size;

        if (trafficMaterial != null && go.TryGetComponent(out MeshRenderer renderer))
        {
            renderer.sharedMaterial = trafficMaterial;
            propertyBlock.Clear();
            propertyBlock.SetColor("_BaseColor", bodyColor);
            renderer.SetPropertyBlock(propertyBlock);
        }

        if (!go.TryGetComponent(out Rigidbody rb))
        {
            rb = go.AddComponent<Rigidbody>();
        }

        rb.isKinematic = true;
        rb.useGravity = false;

        return go.AddComponent<TrafficVehicle>();
    }

    private Color GenerateTrafficColor(System.Random rng)
    {
        // Mostly muted real-world traffic colors, with occasional neutral tones.
        if (rng.NextDouble() < 0.3)
        {
            float v = Mathf.Lerp(0.2f, 0.92f, (float)rng.NextDouble());
            float s = Mathf.Lerp(0f, 0.08f, (float)rng.NextDouble());
            return Color.HSVToRGB(0f, s, v);
        }

        float hue = (float)rng.NextDouble();
        float sat = Mathf.Lerp(0.35f, 0.78f, (float)rng.NextDouble());
        float val = Mathf.Lerp(0.45f, 0.9f, (float)rng.NextDouble());
        return Color.HSVToRGB(hue, sat, val);
    }

    private void EnsureMaterial()
    {
        if (trafficMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            return;
        }

        trafficMaterial = new Material(shader);
        trafficMaterial.name = "TrafficVehicleRuntimeMaterial";
        trafficMaterial.SetFloat("_Smoothness", 0.05f);
        trafficMaterial.SetFloat("_Metallic", 0.0f);
    }

    private void CullOutside(int minChunk, int maxChunk)
    {
        if (chunkVehicles.Count == 0)
        {
            return;
        }

        var rebuilt = new Dictionary<int, List<TrafficVehicle>>(chunkVehicles.Count);
        foreach (KeyValuePair<int, List<TrafficVehicle>> pair in chunkVehicles)
        {
            List<TrafficVehicle> vehicles = pair.Value;
            for (int i = 0; i < vehicles.Count; i++)
            {
                TrafficVehicle vehicle = vehicles[i];
                if (vehicle == null)
                {
                    continue;
                }

                int currentChunk = roadStream.GetChunkIndexForS(vehicle.CurrentS);
                if (currentChunk < minChunk || currentChunk > maxChunk)
                {
                    Destroy(vehicle.gameObject);
                    continue;
                }

                if (!rebuilt.TryGetValue(currentChunk, out List<TrafficVehicle> list))
                {
                    list = new List<TrafficVehicle>();
                    rebuilt[currentChunk] = list;
                }

                list.Add(vehicle);
            }
        }

        chunkVehicles.Clear();
        foreach (KeyValuePair<int, List<TrafficVehicle>> pair in rebuilt)
        {
            chunkVehicles[pair.Key] = pair.Value;
        }
    }

    private static bool IsLaneSpacingValid(List<float> laneS, float candidateS, float minSpacing)
    {
        float minSpacingSq = minSpacing * minSpacing;
        for (int i = 0; i < laneS.Count; i++)
        {
            float d = laneS[i] - candidateS;
            if (d * d < minSpacingSq)
            {
                return false;
            }
        }

        return true;
    }
}
