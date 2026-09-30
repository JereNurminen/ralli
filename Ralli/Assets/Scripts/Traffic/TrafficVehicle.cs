using UnityEngine;

// A traffic car. Follows its lane along the road (kinematic) until it touches the player or
// another traffic car, then is released to regular physics for the rest of its life.
public class TrafficVehicle : MonoBehaviour
{
    private const float KphToMps = 1f / 3.6f;
    private static readonly Color FollowingColor = new Color(0.15f, 0.9f, 0.2f, 1f);
    private static readonly Color BrakingColor = new Color(0.95f, 0.1f, 0.1f, 1f);
    private static readonly Color ReleasedColor = Color.black;
    private static Material debugMarkerMaterial;

    private RoadStreamGenerator road;
    private TrafficConfig config;
    private CarController player;
    private Collider playerCollider;
    private Collider ownCollider;
    private Rigidbody rb;
    // +1 drives along the road (the player's direction, right lane), -1 is oncoming (left lane).
    private float direction;
    private float lateralOffset;
    private float targetSpeedMps;
    private float currentSpeedMps;
    private float currentS;
    private float turnRateDegPerMeter;
    private bool isReleased;
    private bool isBraking;
    private MeshRenderer debugMarker;
    private MaterialPropertyBlock debugMarkerBlock;
    private readonly Collider[] overlapBuffer = new Collider[16];

    public float CurrentS => currentS;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ownCollider = GetComponent<Collider>();
    }

    public void Initialize(RoadStreamGenerator roadGenerator, TrafficConfig trafficConfig, CarController playerCar, float startS, float travelDirection, float speedKph)
    {
        road = roadGenerator;
        config = trafficConfig;
        player = playerCar;
        playerCollider = player != null ? player.GetComponent<Collider>() : null;
        currentS = startS;
        direction = travelDirection >= 0f ? 1f : -1f;
        targetSpeedMps = Mathf.Max(1f, speedKph) * KphToMps;
        currentSpeedMps = targetSpeedMps;
        lateralOffset = GetLaneOffset();

        CreateDebugMarker();
        MoveToRoad();
        UpdateDebugMarker();
    }

    public void Tick(float deltaTime)
    {
        if (isReleased)
        {
            return;
        }

        if (TryReleaseOnContact())
        {
            return;
        }

        float desiredSpeed = targetSpeedMps * GetCornerSpeedFactor();
        isBraking = currentSpeedMps > desiredSpeed + 0.1f;
        float rate = Mathf.Max(0.1f, isBraking ? config.brakingMps2 : config.accelerationMps2);
        currentSpeedMps = Mathf.MoveTowards(currentSpeedMps, desiredSpeed, rate * deltaTime);
        currentS += currentSpeedMps * direction * deltaTime;

        MoveToRoad();
        UpdateDebugMarker();
    }

    // Lane center: a quarter of the usable road width from the centerline, on the travel side.
    private float GetLaneOffset()
    {
        float roadWidth = Mathf.Max(2f, road.GetRoadWidth());
        float usableWidth = Mathf.Max(1f, roadWidth - Mathf.Max(0f, config.laneShoulderInset) * 2f);
        return usableWidth * 0.25f * direction;
    }

    private float GetCornerSpeedFactor()
    {
        if (!config.slowInCorners)
        {
            return 1f;
        }

        float start = Mathf.Max(0.001f, config.cornerSlowStartTurnRateDegPerMeter);
        float end = Mathf.Max(start + 0.001f, config.cornerSlowMaxTurnRateDegPerMeter);
        float t = Mathf.InverseLerp(start, end, Mathf.Abs(turnRateDegPerMeter));
        return Mathf.Lerp(1f, Mathf.Clamp(config.cornerMinSpeedFactor, 0.2f, 1f), t);
    }

    // One road lookup per step: places the car and keeps the turn rate for the next corner check.
    private void MoveToRoad()
    {
        if (!road.TryGetRoadFrameAtS(currentS, out Vector3 position, out Vector3 forward, out Vector3 right, out Vector3 up, out turnRateDegPerMeter))
        {
            return;
        }

        float halfHeight = Mathf.Max(0.1f, transform.localScale.y * 0.5f);
        Vector3 lanePosition = position + right * lateralOffset + up * halfHeight;
        transform.SetPositionAndRotation(lanePosition, Quaternion.LookRotation(forward * direction, up));
    }

    private bool TryReleaseOnContact()
    {
        if (IsNearPlayer())
        {
            Release();
            return true;
        }

        TrafficVehicle other = FindTouchingTraffic();
        if (other != null)
        {
            Release();
            other.Release();
            return true;
        }

        return false;
    }

    // Released just before touching, so the crash is a proper physics collision.
    private bool IsNearPlayer()
    {
        if (player == null || playerCollider == null || ownCollider == null)
        {
            return false;
        }

        float releaseDistance = Mathf.Max(0.05f, config.ragdollReleaseDistance);
        float broadPhase = releaseDistance + 6f;
        if ((player.transform.position - transform.position).sqrMagnitude > broadPhase * broadPhase)
        {
            return false;
        }

        Vector3 ownClosest = ownCollider.ClosestPoint(player.transform.position);
        Vector3 playerClosest = playerCollider.ClosestPoint(transform.position);
        return Vector3.Distance(ownClosest, playerClosest) < releaseDistance;
    }

    // Kinematic cars get no collision events with each other, so overlaps are checked by hand.
    private TrafficVehicle FindTouchingTraffic()
    {
        if (ownCollider == null)
        {
            return null;
        }

        Bounds bounds = ownCollider.bounds;
        int hitCount = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents * 1.02f, overlapBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider other = overlapBuffer[i];
            if (other == ownCollider || !other.TryGetComponent(out TrafficVehicle vehicle))
            {
                continue;
            }

            Vector3 ownClosest = ownCollider.ClosestPoint(other.bounds.center);
            Vector3 otherClosest = other.ClosestPoint(transform.position);
            if ((ownClosest - otherClosest).sqrMagnitude <= 0.0025f)
            {
                return vehicle;
            }
        }

        return null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collider other = collision.collider;
        if (player != null && other.GetComponentInParent<CarController>() == player)
        {
            Release();
            return;
        }

        TrafficVehicle otherVehicle = other.GetComponentInParent<TrafficVehicle>();
        if (otherVehicle != null && otherVehicle != this)
        {
            Release();
            otherVehicle.Release();
        }
    }

    private void Release()
    {
        if (isReleased)
        {
            return;
        }

        isReleased = true;
        isBraking = false;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = transform.forward * currentSpeedMps;
        rb.angularVelocity = Vector3.zero;
        rb.linearDamping = 0.2f;
        rb.angularDamping = 0.3f;
        UpdateDebugMarker();
    }

    // Floating ball above the car: green = following its lane, red = braking, black = released.
    private void CreateDebugMarker()
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "StateMarker";
        Destroy(marker.GetComponent<Collider>());
        marker.transform.SetParent(transform, false);
        marker.transform.localScale = Vector3.one * 0.35f;
        marker.transform.localPosition = new Vector3(0f, Mathf.Max(0.1f, transform.localScale.y * 0.5f) + 0.9f, 0f);

        if (debugMarkerMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                debugMarkerMaterial = new Material(shader) { name = "TrafficDebugMarkerMaterial" };
            }
        }

        debugMarker = marker.GetComponent<MeshRenderer>();
        if (debugMarkerMaterial != null)
        {
            debugMarker.sharedMaterial = debugMarkerMaterial;
        }

        debugMarker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        debugMarker.receiveShadows = false;
        debugMarkerBlock = new MaterialPropertyBlock();
    }

    private void UpdateDebugMarker()
    {
        if (debugMarker == null)
        {
            return;
        }

        Color color = isReleased ? ReleasedColor : isBraking ? BrakingColor : FollowingColor;
        debugMarkerBlock.SetColor("_BaseColor", color);
        debugMarkerBlock.SetColor("_Color", color);
        debugMarker.SetPropertyBlock(debugMarkerBlock);
    }
}
