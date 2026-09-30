using System.Collections.Generic;
using UnityEngine;

public class TrafficStreamManager : MonoBehaviour
{
    [SerializeField] private RoadStreamGenerator roadStream;
    [SerializeField] private TrafficConfig config;
    [SerializeField] private Transform vehicleRoot;

    private readonly List<TrafficVehicle> vehicles = new List<TrafficVehicle>();
    private readonly HashSet<int> spawnedChunks = new HashSet<int>();
    private CarController player;
    private MaterialPropertyBlock propertyBlock;
    private Material trafficMaterial;

    private void Start()
    {
        if (roadStream == null)
        {
            roadStream = FindFirstObjectByType<RoadStreamGenerator>();
        }

        player = FindFirstObjectByType<CarController>();
        propertyBlock = new MaterialPropertyBlock();

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
        float deltaTime = Time.fixedDeltaTime;
        for (int i = 0; i < vehicles.Count; i++)
        {
            vehicles[i].Tick(deltaTime);
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
        float playerS = roadStream.GetEstimatedPlayerS();
        float densityT = Mathf.Clamp01(playerS / Mathf.Max(1f, config.densityRampDistance));
        float densityMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, config.maxDensityMultiplier), densityT);
        float dynamicVehiclesPerKm = Mathf.Max(0f, config.vehiclesPerKilometer) * densityMultiplier;
        float perChunk = chunkLength * 0.001f * dynamicVehiclesPerKm;
        int count = Mathf.Clamp(Mathf.RoundToInt(perChunk), 0, Mathf.Max(0, config.maxVehiclesPerChunk));
        if (count <= 0)
        {
            spawnedChunks.Add(chunkIndex);
            return;
        }

        System.Random rng = new System.Random((roadStream.GetSeed() * 83492791) ^ (chunkIndex * 19349663));
        var forwardLane = new List<float>(count);
        var oncomingLane = new List<float>(count);
        int created = 0;
        int attempts = count * 8;
        while (created < count && attempts-- > 0)
        {
            float s = Mathf.Lerp(startS + 3f, endS - 3f, (float)rng.NextDouble());
            bool forward = rng.NextDouble() < Mathf.Clamp01(config.sameDirectionLaneChance);
            List<float> lane = forward ? forwardLane : oncomingLane;
            if (!IsLaneSpacingValid(lane, s, Mathf.Max(2f, config.sameLaneMinSpacing)))
            {
                continue;
            }

            float speedKph = Mathf.Max(10f, config.trafficSpeedKph + ((float)rng.NextDouble() * 2f - 1f) * Mathf.Max(0f, config.speedVarianceKph));
            TrafficVehicle vehicle = CreateBoxVehicle($"Traffic_{chunkIndex}_{created:00}", GenerateTrafficColor(rng));
            vehicle.Initialize(roadStream, config, player, s, forward ? 1f : -1f, speedKph);
            vehicles.Add(vehicle);
            lane.Add(s);
            created++;
        }

        spawnedChunks.Add(chunkIndex);
    }

    private TrafficVehicle CreateBoxVehicle(string name, Color bodyColor)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(vehicleRoot, true);

        go.transform.localScale = config.vehicleBoxSize;

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
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.mass = Mathf.Max(100f, config.vehicleMassKg);
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

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
        for (int i = vehicles.Count - 1; i >= 0; i--)
        {
            TrafficVehicle vehicle = vehicles[i];
            int chunk = roadStream.GetChunkIndexForS(vehicle.CurrentS);
            if (chunk < minChunk || chunk > maxChunk)
            {
                Destroy(vehicle.gameObject);
                vehicles.RemoveAt(i);
            }
        }
    }

    private static bool IsLaneSpacingValid(List<float> laneS, float candidateS, float minSpacing)
    {
        for (int i = 0; i < laneS.Count; i++)
        {
            if (Mathf.Abs(laneS[i] - candidateS) < minSpacing)
            {
                return false;
            }
        }

        return true;
    }
}
