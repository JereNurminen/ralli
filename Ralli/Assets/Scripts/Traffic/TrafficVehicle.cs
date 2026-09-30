using UnityEngine;

// A traffic car. Follows its lane along the road (kinematic), keeping a gap to the car ahead in
// its lane. It is released to regular physics for the rest of its life when it touches the player
// or a wreck, or when it panics: the player about to hit it head-on in its lane makes it flinch
// toward the road edge and skid to a stop (it is despawned soon after the player passes anyway).
public class TrafficVehicle : MonoBehaviour
{
    // The player in road coordinates, computed once per physics step by the manager.
    public struct PlayerOnRoad
    {
        public bool valid;
        public float s;
        // Sideways offset from the centerline, along the road's right vector (m).
        public float lateral;
        // Velocity along the road direction of increasing s (m/s).
        public float speedAlongRoad;
    }

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
    private TrafficVehicle leader;
    private MeshRenderer debugMarker;
    private MaterialPropertyBlock debugMarkerBlock;

    public float CurrentS => currentS;
    public float Direction => direction;
    public bool IsReleased => isReleased;

    // The next car ahead in the same lane (set by the manager every step), or null.
    public void SetLeader(TrafficVehicle carAhead)
    {
        leader = carAhead;
    }

    // Released cars leave their lane; keep their road position current (for culling) from where they are.
    public void RefreshReleasedRoadPosition(float maxRoadDistance)
    {
        if (isReleased && road.TryGetNearestS(transform.position, maxRoadDistance, out float s))
        {
            currentS = s;
        }
    }

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

        if (config.showStateMarkers)
        {
            CreateDebugMarker();
        }

        MoveToRoad(true);
        UpdateDebugMarker();
    }

    public void Tick(float deltaTime, in PlayerOnRoad playerOnRoad)
    {
        if (isReleased)
        {
            return;
        }

        if (IsNearPlayer())
        {
            Release();
            return;
        }

        if (IsAboutToBeHit(playerOnRoad))
        {
            Panic();
            return;
        }

        float desiredSpeed = Mathf.Min(targetSpeedMps * GetCornerSpeedFactor(), GetFollowSpeedLimit());
        isBraking = currentSpeedMps > desiredSpeed + 0.1f;
        float rate = Mathf.Max(0.1f, isBraking ? config.brakingMps2 : config.accelerationMps2);
        currentSpeedMps = Mathf.MoveTowards(currentSpeedMps, desiredSpeed, rate * deltaTime);
        currentS += currentSpeedMps * direction * deltaTime;

        MoveToRoad(false);
        UpdateDebugMarker();
    }

    // The player is ahead in this car's travel direction, inside its lane, closing in, and a
    // head-on collision is about a second away. Only the player triggers this.
    private bool IsAboutToBeHit(in PlayerOnRoad playerOnRoad)
    {
        if (!playerOnRoad.valid)
        {
            return false;
        }

        float distanceAhead = (playerOnRoad.s - currentS) * direction - transform.localScale.z;
        float closingSpeed = currentSpeedMps - playerOnRoad.speedAlongRoad * direction;
        bool inLane = Mathf.Abs(playerOnRoad.lateral - lateralOffset) < Mathf.Abs(lateralOffset) + config.panicLaneMargin;
        return distanceAhead >= 0f && inLane && closingSpeed >= config.panicMinClosingSpeed
               && distanceAhead / closingSpeed <= config.panicTimeToCollision;
    }

    // Flinch toward this car's own road edge and skid to a stop as a physics object.
    private void Panic()
    {
        Release();
        Vector3 towardEdge = transform.right * Mathf.Sign(lateralOffset) * direction;
        rb.linearVelocity += towardEdge * config.panicSwerveSpeed;
        rb.angularVelocity = transform.up * (Mathf.Sign(lateralOffset) * direction * config.panicSwerveYawRate * Mathf.Deg2Rad);
        rb.linearDamping = config.panicBrakeDrag;
    }

    // Lane center: a quarter of the usable road width from the centerline, on the travel side.
    private float GetLaneOffset()
    {
        float roadWidth = Mathf.Max(2f, road.GetRoadWidth());
        float usableWidth = Mathf.Max(1f, roadWidth - Mathf.Max(0f, config.laneShoulderInset) * 2f);
        return usableWidth * 0.25f * direction;
    }

    // Speed that holds a gap of minGap + timeGap * speed to the car ahead: closes in slower than
    // the leader when too near, catches up when far. No limit without a leader.
    private float GetFollowSpeedLimit()
    {
        if (leader == null || leader.isReleased)
        {
            return float.MaxValue;
        }

        float length = transform.localScale.z;
        float gap = (leader.currentS - currentS) * direction - length;
        float wantedGap = config.followMinGap + currentSpeedMps * config.followTimeGap;
        return Mathf.Max(0f, leader.currentSpeedMps + (gap - wantedGap) * config.followGapGain);
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
    // Moves through the physics engine (not by setting the transform) so the kinematic body has a
    // real velocity and collisions with the player respond to it properly.
    private void MoveToRoad(bool teleport)
    {
        if (!road.TryGetRoadFrameAtS(currentS, out Vector3 position, out Vector3 forward, out Vector3 right, out Vector3 up, out turnRateDegPerMeter))
        {
            return;
        }

        float halfHeight = Mathf.Max(0.1f, transform.localScale.y * 0.5f);
        Vector3 lanePosition = position + right * lateralOffset + up * halfHeight;
        Quaternion rotation = Quaternion.LookRotation(forward * direction, up);
        if (teleport)
        {
            transform.SetPositionAndRotation(lanePosition, rotation);
            rb.position = lanePosition;
            rb.rotation = rotation;
            return;
        }

        rb.MovePosition(lanePosition);
        rb.MoveRotation(rotation);
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
