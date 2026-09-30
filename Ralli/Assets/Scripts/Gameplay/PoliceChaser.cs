using System;
using UnityEngine;

// The police: after a delay a van appears on the road behind the player and chases at a constant
// speed along the road. It never dodges; traffic it touches is knocked loose and shoved aside.
// Near the player it steers onto the player's line; at the player's bumper it matches their speed
// and fires CaughtPlayer (the future lose condition).
public class PoliceChaser : MonoBehaviour
{
    private const float PlayerHalfLength = 1.9f;

    [SerializeField] private PoliceConfig config;
    [SerializeField] private RoadStreamGenerator road;

    private CarController player;
    private Rigidbody playerBody;
    private Rigidbody van;
    private BoxCollider vanCollider;
    private float startTimer;
    private float s;
    private float speedMps;
    private float lateralOffset;
    private readonly Collider[] shoveBuffer = new Collider[16];

    public event Action CaughtPlayer;
    public bool IsChasing => van != null;
    public bool HasCaughtPlayer { get; private set; }
    // Bumper-to-bumper distance to the player along the road (m); meaningful while chasing.
    public float GapToPlayer { get; private set; }

    private void Start()
    {
        if (road == null)
        {
            road = FindFirstObjectByType<RoadStreamGenerator>();
        }

        player = FindFirstObjectByType<CarController>();
        playerBody = player != null ? player.GetComponent<Rigidbody>() : null;
    }

    private void FixedUpdate()
    {
        if (config == null || road == null || player == null || !road.IsReady)
        {
            return;
        }

        float deltaTime = Time.fixedDeltaTime;
        if (van == null)
        {
            startTimer += deltaTime;
            if (startTimer >= config.startDelay)
            {
                SpawnVan();
            }

            return;
        }

        Chase(deltaTime);
        ShoveTraffic();
    }

    private void SpawnVan()
    {
        s = Mathf.Max(0f, road.GetEstimatedPlayerS() - config.spawnDistanceBehind);
        speedMps = config.chaseSpeedKph / 3.6f;
        lateralOffset = GetLaneOffset();

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "PoliceVan";
        body.transform.SetParent(transform, false);
        body.transform.localScale = config.vanSize;
        vanCollider = body.GetComponent<BoxCollider>();

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            var material = new Material(shader) { name = "PoliceVanMaterial", color = config.vanColor };
            material.SetFloat("_Smoothness", 0.1f);
            body.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        van = body.AddComponent<Rigidbody>();
        van.isKinematic = true;
        van.useGravity = false;
        van.interpolation = RigidbodyInterpolation.Interpolate;
        van.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        MoveAlongRoad(true);
    }

    private void Chase(float deltaTime)
    {
        float playerS = road.GetEstimatedPlayerS();
        GapToPlayer = playerS - s - config.vanSize.z * 0.5f - PlayerHalfLength;

        float targetSpeed = config.chaseSpeedKph / 3.6f;
        float playerLateral = lateralOffset;
        if (road.TryGetRoadFrameAtS(playerS, out Vector3 playerRoadPoint, out Vector3 forward, out Vector3 right, out _, out _))
        {
            playerLateral = Vector3.Dot(player.transform.position - playerRoadPoint, right);
            if (GapToPlayer <= config.catchDistance)
            {
                // Caught: sit on the player's bumper instead of driving through them.
                targetSpeed = Mathf.Min(targetSpeed, Mathf.Max(0f, Vector3.Dot(playerBody.linearVelocity, forward)));
                if (!HasCaughtPlayer)
                {
                    HasCaughtPlayer = true;
                    Debug.Log("[PoliceChaser] Caught the player.");
                    CaughtPlayer?.Invoke();
                }
            }
        }

        speedMps = Mathf.MoveTowards(speedMps, targetSpeed, config.accelerationMps2 * deltaTime);
        s += speedMps * deltaTime;

        // Blend from its own lane onto the player's line as it closes in, staying on the road.
        float homing = 1f - Mathf.Clamp01(GapToPlayer / Mathf.Max(1f, config.homingDistance));
        float edge = road.GetRoadWidth() * 0.5f - config.vanSize.x * 0.5f;
        float lateralTarget = Mathf.Clamp(Mathf.Lerp(GetLaneOffset(), playerLateral, homing), -edge, edge);
        lateralOffset = Mathf.MoveTowards(lateralOffset, lateralTarget, config.lateralSpeed * deltaTime);

        MoveAlongRoad(false);
    }

    // Right-hand lane center, like traffic driving the player's direction.
    private float GetLaneOffset()
    {
        return road.GetRoadWidth() * 0.25f;
    }

    private void MoveAlongRoad(bool teleport)
    {
        if (!road.TryGetRoadFrameAtS(s, out Vector3 position, out Vector3 forward, out Vector3 right, out Vector3 up, out _))
        {
            return;
        }

        Vector3 vanPosition = position + right * lateralOffset + up * (config.vanSize.y * 0.5f);
        Quaternion rotation = Quaternion.LookRotation(forward, up);
        if (teleport)
        {
            van.transform.SetPositionAndRotation(vanPosition, rotation);
            van.position = vanPosition;
            van.rotation = rotation;
            return;
        }

        van.MovePosition(vanPosition);
        van.MoveRotation(rotation);
    }

    // Kinematic traffic can't be pushed, so anything the van touches is released to physics first.
    private void ShoveTraffic()
    {
        Vector3 halfExtents = config.vanSize * 0.5f + Vector3.one * config.shovePadding;
        int hits = Physics.OverlapBoxNonAlloc(van.position, halfExtents, shoveBuffer, van.rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            if (shoveBuffer[i] != vanCollider && shoveBuffer[i].TryGetComponent(out TrafficVehicle traffic))
            {
                traffic.Release();
            }
        }
    }
}
