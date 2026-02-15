using UnityEngine;

public class TrafficVehicle : MonoBehaviour
{
    private const float KphToMps = 1f / 3.6f;

    private RoadStreamGenerator road;
    private TrafficConfig config;
    private float laneSign;
    private float directionSign;
    private float lateralOffset;
    private float targetSpeedMps;
    private float currentSpeedMps;
    private float currentS;
    private bool isInitialized;

    public float CurrentS => currentS;
    public float CurrentSpeedMps => currentSpeedMps;
    public bool IsBraking { get; private set; }

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

        UpdatePose();
    }

    public void Tick(float dt)
    {
        if (!isInitialized || road == null || config == null)
        {
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
        if (!road.TryGetRoadFrameAtS(currentS, out Vector3 pos, out Vector3 forward, out Vector3 right, out _, out _))
        {
            return;
        }

        Vector3 lanePosition = pos + right * lateralOffset;
        Vector3 facing = directionSign > 0f ? forward : -forward;
        if (facing.sqrMagnitude < 0.0001f)
        {
            facing = transform.forward;
        }

        transform.SetPositionAndRotation(lanePosition, Quaternion.LookRotation(facing.normalized, Vector3.up));
    }
}
