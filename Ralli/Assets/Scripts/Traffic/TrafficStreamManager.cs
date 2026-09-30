using System.Collections.Generic;
using UnityEngine;

public class TrafficStreamManager : MonoBehaviour
{
    [SerializeField] private RoadStreamGenerator roadStream;
    [SerializeField] private TrafficConfig config;
    [SerializeField] private Transform vehicleRoot;

    private const float ReleasedRoadSearchDistance = 40f;
    private static readonly System.Comparison<TrafficVehicle> ByRoadPosition = (a, b) => a.CurrentS.CompareTo(b.CurrentS);
    // Oncoming cars first, then nearest.
    private static readonly System.Comparison<TrafficVehicle> ByHeadlightPriority = (a, b) =>
    {
        int oncoming = a.Direction.CompareTo(b.Direction);
        return oncoming != 0 ? oncoming : a.CurrentS.CompareTo(b.CurrentS);
    };

    private readonly List<TrafficVehicle> vehicles = new List<TrafficVehicle>();
    private readonly List<TrafficVehicle> headlightCandidates = new List<TrafficVehicle>();
    private readonly List<HeadlightSlot> headlightSlots = new List<HeadlightSlot>();
    private readonly List<TrafficVehicle> forwardLane = new List<TrafficVehicle>();
    private readonly List<TrafficVehicle> oncomingLane = new List<TrafficVehicle>();
    private readonly HashSet<int> spawnedChunks = new HashSet<int>();
    private CarController player;
    // Random per scene load: traffic is not tied to the road seed.
    private int trafficSeed;

    public IReadOnlyList<TrafficVehicle> Vehicles => vehicles;

    public TrafficConfig Config => config;

    // Stage setup: a runtime config copy, set before traffic spawns.
    public void UseConfig(TrafficConfig stageConfig)
    {
        config = stageConfig;
    }

    // A pooled real spot light, lent to one traffic car at a time and faded in/out when it moves.
    private class HeadlightSlot
    {
        public Light light;
        public TrafficVehicle car;
        public float weight;
        public bool releasing;
    }
    private Rigidbody playerBody;
    private MaterialPropertyBlock propertyBlock;
    private Material trafficMaterial;

    private void Start()
    {
        if (roadStream == null)
        {
            roadStream = FindFirstObjectByType<RoadStreamGenerator>();
        }

        player = FindFirstObjectByType<CarController>();
        playerBody = player != null ? player.GetComponent<Rigidbody>() : null;
        propertyBlock = new MaterialPropertyBlock();
        trafficSeed = Random.Range(1, int.MaxValue);

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
        AssignRealHeadlights();
    }

    private void LateUpdate()
    {
        UpdateRealHeadlights();
    }

    // Lends the pooled lights to the nearest cars ahead of the player (oncoming first). Cars keep
    // their light while they still qualify, so lights only move when they have to.
    private void AssignRealHeadlights()
    {
        EnsureHeadlightPool();
        float playerS = roadStream.GetEstimatedPlayerS();
        headlightCandidates.Clear();
        for (int i = 0; i < vehicles.Count; i++)
        {
            TrafficVehicle vehicle = vehicles[i];
            float ahead = vehicle.CurrentS - playerS;
            if (!vehicle.IsReleased && ahead > 0f && ahead < config.realHeadlightMaxDistance)
            {
                headlightCandidates.Add(vehicle);
            }
        }

        headlightCandidates.Sort(ByHeadlightPriority);
        int wanted = Mathf.Min(headlightSlots.Count, headlightCandidates.Count);

        foreach (HeadlightSlot slot in headlightSlots)
        {
            if (slot.car != null && !slot.releasing && headlightCandidates.IndexOf(slot.car) >= wanted)
            {
                slot.releasing = true;
            }
        }

        for (int i = 0; i < wanted; i++)
        {
            TrafficVehicle car = headlightCandidates[i];
            if (FindSlot(car) != null)
            {
                continue;
            }

            HeadlightSlot free = FindSlot(null);
            if (free != null)
            {
                free.car = car;
                free.weight = 0f;
                free.releasing = false;
            }
        }
    }

    private HeadlightSlot FindSlot(TrafficVehicle car)
    {
        for (int i = 0; i < headlightSlots.Count; i++)
        {
            if (headlightSlots[i].car == car)
            {
                return headlightSlots[i];
            }
        }

        return null;
    }

    // Fades lights in/out and keeps each one on the front of its car (after interpolation).
    private void UpdateRealHeadlights()
    {
        if (config == null)
        {
            return;
        }

        float fadeStep = Time.deltaTime / Mathf.Max(0.01f, config.realHeadlightFadeTime);
        foreach (HeadlightSlot slot in headlightSlots)
        {
            bool alive = slot.car != null && !slot.car.IsReleased;
            slot.weight = Mathf.MoveTowards(slot.weight, alive && !slot.releasing ? 1f : 0f, fadeStep);
            if (slot.weight <= 0f && (slot.releasing || !alive))
            {
                slot.car = null;
                slot.releasing = false;
            }

            slot.light.enabled = slot.car != null && slot.weight > 0f;
            if (!slot.light.enabled)
            {
                continue;
            }

            Transform car = slot.car.transform;
            slot.light.transform.SetPositionAndRotation(car.TransformPoint(0f, -0.1f, 0.52f), car.rotation * Quaternion.Euler(4f, 0f, 0f));
            slot.light.intensity = config.realHeadlightIntensity * slot.weight;
        }
    }

    private void EnsureHeadlightPool()
    {
        while (headlightSlots.Count < config.realHeadlightCount)
        {
            var lightObject = new GameObject($"TrafficHeadlight_{headlightSlots.Count}");
            lightObject.transform.SetParent(transform, false);
            Light spot = lightObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = config.vehicleLights != null ? config.vehicleLights.headlightColor : Color.white;
            spot.range = config.realHeadlightRange;
            spot.spotAngle = config.realHeadlightAngle;
            spot.innerSpotAngle = config.realHeadlightAngle * 0.5f;
            spot.shadows = LightShadows.None;
            spot.enabled = false;
            lightObject.AddComponent<LightCone>().Configure(config.realHeadlightBeamVisibility, 0.5f);
            headlightSlots.Add(new HeadlightSlot { light = spot });
        }
    }

    private void FixedUpdate()
    {
        // The run starts when the player turns the engine on; until then traffic stands frozen.
        if (player != null && !player.EngineRunning)
        {
            return;
        }

        AssignLeaders();
        TrafficVehicle.PlayerOnRoad playerOnRoad = GetPlayerOnRoad();
        float deltaTime = Time.fixedDeltaTime;
        for (int i = 0; i < vehicles.Count; i++)
        {
            vehicles[i].Tick(deltaTime, playerOnRoad);
        }
    }

    private TrafficVehicle.PlayerOnRoad GetPlayerOnRoad()
    {
        var state = new TrafficVehicle.PlayerOnRoad();
        if (player == null || playerBody == null || vehicles.Count == 0)
        {
            return state;
        }

        state.s = roadStream.GetEstimatedPlayerS();
        if (!roadStream.TryGetRoadFrameAtS(state.s, out Vector3 position, out Vector3 forward, out Vector3 right, out _, out _))
        {
            return state;
        }

        state.valid = true;
        state.lateral = Vector3.Dot(player.transform.position - position, right);
        state.speedAlongRoad = Vector3.Dot(playerBody.linearVelocity, forward);
        return state;
    }

    // Orders each lane's lane-following cars in travel order and points every car at the one ahead.
    private void AssignLeaders()
    {
        forwardLane.Clear();
        oncomingLane.Clear();
        for (int i = 0; i < vehicles.Count; i++)
        {
            TrafficVehicle vehicle = vehicles[i];
            if (!vehicle.IsReleased)
            {
                (vehicle.Direction > 0f ? forwardLane : oncomingLane).Add(vehicle);
            }
        }

        forwardLane.Sort(ByRoadPosition);
        oncomingLane.Sort(ByRoadPosition);
        oncomingLane.Reverse();
        LinkLeaders(forwardLane);
        LinkLeaders(oncomingLane);
    }

    private static void LinkLeaders(List<TrafficVehicle> lane)
    {
        for (int i = 0; i < lane.Count; i++)
        {
            lane[i].SetLeader(i + 1 < lane.Count ? lane[i + 1] : null);
        }
    }

    private void SpawnChunkTraffic(int chunkIndex)
    {
        if (!roadStream.TryGetChunkRangeS(chunkIndex, out float startS, out float endS))
        {
            spawnedChunks.Add(chunkIndex);
            return;
        }

        // Only on the stage road between the stations.
        if (roadStream.TryGetTrafficRange(out float minS, out float maxS))
        {
            startS = Mathf.Max(startS, minS);
            endS = Mathf.Min(endS, maxS);
            if (endS - startS < 10f)
            {
                spawnedChunks.Add(chunkIndex);
                return;
            }
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

        System.Random rng = new System.Random((trafficSeed * 83492791) ^ (chunkIndex * 19349663));
        var forwardSpawns = new List<float>(count);
        var oncomingSpawns = new List<float>(count);
        foreach (TrafficVehicle existing in vehicles)
        {
            if (!existing.IsReleased)
            {
                (existing.Direction > 0f ? forwardSpawns : oncomingSpawns).Add(existing.CurrentS);
            }
        }
        int created = 0;
        int attempts = count * 8;
        while (created < count && attempts-- > 0)
        {
            float s = Mathf.Lerp(startS + 3f, endS - 3f, (float)rng.NextDouble());
            bool forward = rng.NextDouble() < Mathf.Clamp01(config.sameDirectionLaneChance);
            List<float> lane = forward ? forwardSpawns : oncomingSpawns;
            if (Mathf.Abs(s - playerS) < config.minSpawnDistanceFromPlayer
                || !IsLaneSpacingValid(lane, s, Mathf.Max(2f, config.sameLaneMinSpacing)))
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

        if (config.vehicleLights != null)
        {
            go.AddComponent<VehicleLights>().Build(config.vehicleLights, config.vehicleBoxSize, true);
        }

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

    // Cars leave when their chunk is out of range, or when they drive off the stage road (into a
    // station stretch).
    private void CullOutside(int minChunk, int maxChunk)
    {
        bool hasRange = roadStream.TryGetTrafficRange(out float minS, out float maxS);
        for (int i = vehicles.Count - 1; i >= 0; i--)
        {
            TrafficVehicle vehicle = vehicles[i];
            vehicle.RefreshReleasedRoadPosition(ReleasedRoadSearchDistance);
            int chunk = roadStream.GetChunkIndexForS(vehicle.CurrentS);
            bool offStageRoad = hasRange && !vehicle.IsReleased && (vehicle.CurrentS < minS || vehicle.CurrentS > maxS);
            if (chunk < minChunk || chunk > maxChunk || offStageRoad)
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
