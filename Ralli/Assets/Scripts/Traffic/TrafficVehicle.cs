using UnityEngine;

public class TrafficVehicle : MonoBehaviour
{
    private const float KphToMps = 1f / 3.6f;
    private const float DefaultProximityReleaseDistanceMeters = 0.5f;
    private static readonly Color TrackFollowColor = new Color(0.15f, 0.9f, 0.2f, 1f);
    private static readonly Color BrakingColor = new Color(0.95f, 0.1f, 0.1f, 1f);
    private static readonly Color ReleasedColor = Color.black;
    private static Material debugMarkerSharedMaterial;

    private RoadStreamGenerator road;
    private TrafficConfig config;
    private CarController playerCar;
    private Collider playerCollider;
    private Collider ownCollider;
    private Rigidbody rb;
    private float laneSign;
    private float directionSign;
    private float lateralOffset;
    private float targetSpeedMps;
    private float currentSpeedMps;
    private float currentS;
    private bool isInitialized;
    private bool isReleasedToPhysics;
    private Transform debugMarker;
    private MeshRenderer debugMarkerRenderer;
    private MaterialPropertyBlock debugMarkerBlock;
    private readonly Collider[] trafficContactBuffer = new Collider[16];

    public float CurrentS => currentS;
    public float CurrentSpeedMps => currentSpeedMps;
    public bool IsBraking { get; private set; }
    public bool IsTrackFollowing => !isReleasedToPhysics;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ownCollider = GetComponent<Collider>();
    }

    public void Initialize(
        RoadStreamGenerator roadGenerator,
        TrafficConfig trafficConfig,
        float startS,
        float laneSideSign,
        float travelDirectionSign,
        float initialSpeedKph)
    {
        road = roadGenerator;
        config = trafficConfig;
        playerCar = FindFirstObjectByType<CarController>();
        playerCollider = playerCar != null ? playerCar.GetComponent<Collider>() : null;
        currentS = startS;
        laneSign = Mathf.Sign(laneSideSign);
        directionSign = Mathf.Sign(travelDirectionSign);
        if (Mathf.Abs(directionSign) < 0.5f)
        {
            directionSign = 1f;
        }

        targetSpeedMps = Mathf.Max(1f, initialSpeedKph) * KphToMps;
        currentSpeedMps = targetSpeedMps;
        lateralOffset = ComputeLaneOffset();
        isInitialized = true;
        isReleasedToPhysics = false;
        IsBraking = false;

        EnsureDebugMarker();
        UpdatePose();
        UpdateDebugMarkerVisual();
    }

    public void Tick(float dt)
    {
        if (!isInitialized || road == null || config == null)
        {
            return;
        }

        if (isReleasedToPhysics)
        {
            UpdateReleasedPhysicsState();
            UpdateDebugMarkerVisual();
            return;
        }

        EvaluateTrafficContactRelease();
        if (isReleasedToPhysics)
        {
            UpdateReleasedPhysicsState();
            UpdateDebugMarkerVisual();
            return;
        }

        EvaluateProximityRelease();
        if (isReleasedToPhysics)
        {
            UpdateReleasedPhysicsState();
            UpdateDebugMarkerVisual();
            return;
        }

        float desiredSpeed = targetSpeedMps;
        if (config.slowInCorners && road.TryGetRoadFrameAtS(currentS, out _, out _, out _, out _, out float turnRateDegPerMeter))
        {
            float absTurn = Mathf.Abs(turnRateDegPerMeter);
            float t = Mathf.InverseLerp(
                Mathf.Max(0.001f, config.cornerSlowStartTurnRateDegPerMeter),
                Mathf.Max(config.cornerSlowStartTurnRateDegPerMeter + 0.001f, config.cornerSlowMaxTurnRateDegPerMeter),
                absTurn
            );
            float cornerFactor = Mathf.Lerp(1f, Mathf.Clamp(config.cornerMinSpeedFactor, 0.2f, 1f), t);
            desiredSpeed *= cornerFactor;
        }

        bool braking = currentSpeedMps > desiredSpeed + 0.1f;
        float rate = braking ? Mathf.Max(0.1f, config.brakingMps2) : Mathf.Max(0.1f, config.accelerationMps2);
        currentSpeedMps = Mathf.MoveTowards(currentSpeedMps, desiredSpeed, rate * dt);
        IsBraking = braking;

        currentS += currentSpeedMps * directionSign * dt;
        UpdatePose();
        UpdateDebugMarkerVisual();
    }

    private float ComputeLaneOffset()
    {
        if (road == null)
        {
            return 0f;
        }

        float roadWidth = Mathf.Max(2f, road.GetRoadWidth());
        float inset = config != null ? Mathf.Max(0f, config.laneShoulderInset) : 0f;
        float effectiveWidth = Mathf.Max(1f, roadWidth - inset * 2f);
        float laneCenter = effectiveWidth * 0.25f;
        return laneCenter * laneSign;
    }

    private void UpdatePose()
    {
        if (!road.TryGetRoadFrameAtS(currentS, out Vector3 pos, out Vector3 forward, out Vector3 right, out Vector3 up, out _))
        {
            return;
        }

        float halfHeight = Mathf.Max(0.1f, transform.localScale.y * 0.5f);
        Vector3 lanePosition = pos + right * lateralOffset + up.normalized * halfHeight;
        Vector3 facing = directionSign > 0f ? forward : -forward;
        if (facing.sqrMagnitude < 0.0001f)
        {
            facing = transform.forward;
        }

        transform.SetPositionAndRotation(lanePosition, Quaternion.LookRotation(facing.normalized, up.normalized));
    }

    private void EvaluateProximityRelease()
    {
        if (playerCar == null)
        {
            return;
        }

        float releaseDistance = config != null
            ? Mathf.Max(0.05f, config.ragdollReleaseDistance)
            : DefaultProximityReleaseDistanceMeters;

        if (playerCollider == null)
        {
            playerCollider = playerCar.GetComponent<Collider>();
        }

        if (ownCollider == null)
        {
            ownCollider = GetComponent<Collider>();
        }

        if (ownCollider != null && playerCollider != null)
        {
            Vector3 ownClosest = ownCollider.ClosestPoint(playerCar.transform.position);
            Vector3 playerClosest = playerCollider.ClosestPoint(transform.position);
            float proximityDistance = Vector3.Distance(ownClosest, playerClosest);
            if (proximityDistance < releaseDistance)
            {
                ReleaseToPhysics();
            }
        }
        else
        {
            float proximityDistance = Vector3.Distance(transform.position, playerCar.transform.position);
            if (proximityDistance < releaseDistance)
            {
                ReleaseToPhysics();
            }
        }
    }

    private void EvaluateTrafficContactRelease()
    {
        if (ownCollider == null)
        {
            ownCollider = GetComponent<Collider>();
        }

        if (ownCollider == null)
        {
            return;
        }

        Bounds bounds = ownCollider.bounds;
        Vector3 extents = bounds.extents * 1.02f;
        int hitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            extents,
            trafficContactBuffer,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider otherCollider = trafficContactBuffer[i];
            if (otherCollider == null || otherCollider == ownCollider)
            {
                continue;
            }

            if (otherCollider.attachedRigidbody == rb)
            {
                continue;
            }

            TrafficVehicle otherVehicle = otherCollider.GetComponentInParent<TrafficVehicle>();
            if (otherVehicle == null || otherVehicle == this)
            {
                continue;
            }

            Vector3 ownClosest = ownCollider.ClosestPoint(otherCollider.bounds.center);
            Vector3 otherClosest = otherCollider.ClosestPoint(transform.position);
            float separationSq = (ownClosest - otherClosest).sqrMagnitude;
            if (separationSq > 0.0025f) // 5 cm tolerance
            {
                continue;
            }

            ReleaseToPhysics();
            otherVehicle.ReleaseToPhysics();
            return;
        }
    }

    private void ReleaseToPhysics()
    {
        if (isReleasedToPhysics)
        {
            return;
        }

        isReleasedToPhysics = true;
        IsBraking = false;

        if (rb == null)
        {
            return;
        }

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = transform.forward * currentSpeedMps;
        rb.angularVelocity = Vector3.zero;
        rb.linearDamping = 0.2f;
        rb.angularDamping = 0.3f;

        UpdateDebugMarkerVisual();
    }

    private void UpdateReleasedPhysicsState()
    {
        if (rb == null)
        {
            return;
        }

        IsBraking = false;
        UpdateDebugMarkerVisual();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        if (playerCar == null)
        {
            playerCar = FindFirstObjectByType<CarController>();
        }

        if (playerCar != null && collision.collider != null)
        {
            if (collision.collider.gameObject == playerCar.gameObject || collision.collider.GetComponentInParent<CarController>() == playerCar)
            {
                ReleaseToPhysics();
                return;
            }
        }

        if (collision.collider == null)
        {
            return;
        }

        TrafficVehicle otherVehicle = collision.collider.GetComponentInParent<TrafficVehicle>();
        if (otherVehicle != null && otherVehicle != this)
        {
            ReleaseToPhysics();
            otherVehicle.ReleaseToPhysics();
        }
    }

    private void EnsureDebugMarker()
    {
        if (debugMarker != null && debugMarkerRenderer != null)
        {
            return;
        }

        Transform existing = transform.Find("StateMarker");
        if (existing != null)
        {
            debugMarker = existing;
            debugMarkerRenderer = existing.GetComponent<MeshRenderer>();
        }
        else
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "StateMarker";
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = Vector3.one * 0.35f;
            debugMarker = marker.transform;
            debugMarkerRenderer = marker.GetComponent<MeshRenderer>();

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        if (debugMarkerBlock == null)
        {
            debugMarkerBlock = new MaterialPropertyBlock();
        }

        if (debugMarkerRenderer == null)
        {
            return;
        }

        if (debugMarkerSharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                debugMarkerSharedMaterial = new Material(shader)
                {
                    name = "TrafficDebugMarkerMaterial"
                };
            }
        }

        if (debugMarkerSharedMaterial != null)
        {
            debugMarkerRenderer.sharedMaterial = debugMarkerSharedMaterial;
        }

        debugMarkerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        debugMarkerRenderer.receiveShadows = false;
    }

    private void UpdateDebugMarkerVisual()
    {
        if (debugMarker == null || debugMarkerRenderer == null)
        {
            return;
        }

        float halfHeight = Mathf.Max(0.1f, transform.localScale.y * 0.5f);
        debugMarker.localPosition = new Vector3(0f, halfHeight + 0.9f, 0f);
        debugMarker.localRotation = Quaternion.identity;

        Color color = GetStateColor();
        debugMarkerBlock.Clear();
        debugMarkerBlock.SetColor("_BaseColor", color);
        debugMarkerBlock.SetColor("_Color", color);
        debugMarkerRenderer.SetPropertyBlock(debugMarkerBlock);
    }

    private Color GetStateColor()
    {
        if (isReleasedToPhysics)
        {
            return IsBraking ? BrakingColor : ReleasedColor;
        }

        return IsBraking ? BrakingColor : TrackFollowColor;
    }
}
