using System;
using System.Collections.Generic;
using UnityEngine;

// Style scoring. Tricks (drift, near pass, near miss, big/insane speed) award flat points times the
// current multiplier, banked into the score immediately. The multiplier is
//   1 + speed bonus (current speed) + trick bonus (grows per trick) + police bonus (van distance).
// Hits only cost multiplier: scrapes and big hits shave the trick bonus, major collisions zero it.
public class ScoreSystem : MonoBehaviour
{
    private const float PlayerHalfWidth = 0.8f;

    [SerializeField] private ScoreConfig config;

    private CarController player;
    private Rigidbody playerBody;
    private TrafficStreamManager traffic;
    private PoliceChaser police;
    private readonly Dictionary<TrafficVehicle, float> lastAlongOffset = new Dictionary<TrafficVehicle, float>();
    private readonly List<TrafficVehicle> staleVehicles = new List<TrafficVehicle>();

    private bool drifting;
    private float driftTime;
    private float driftGraceTimer;
    private float bigSpeedTimer;
    private float insaneSpeedTimer;
    private bool bigSpeedArmed = true;
    private bool insaneSpeedArmed = true;

    public float Score { get; private set; }
    public float TrickBonus { get; private set; }
    public float Multiplier { get; private set; } = 1f;

    // Trick name and points awarded (already multiplied).
    public event Action<string, float> TrickScored;
    // Multiplier-affecting hit: short label (e.g. "SCRAPE") and the trick bonus lost.
    public event Action<string, float> MultiplierLost;

    private void Start()
    {
        player = FindFirstObjectByType<CarController>();
        playerBody = player != null ? player.GetComponent<Rigidbody>() : null;
        traffic = FindFirstObjectByType<TrafficStreamManager>();
        police = FindFirstObjectByType<PoliceChaser>();
        if (player != null)
        {
            player.Impact += OnPlayerImpact;
        }
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.Impact -= OnPlayerImpact;
        }
    }

    private void Update()
    {
        if (config == null || player == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        float speedKph = player.SpeedMps * 3.6f;
        Multiplier = 1f + GetSpeedBonus(speedKph) + TrickBonus + GetPoliceBonus();

        UpdateDrift(speedKph, deltaTime);
        UpdateSpeedTricks(speedKph, deltaTime);
        UpdateTrafficPasses();
    }

    private float GetSpeedBonus(float speedKph)
    {
        return speedKph / 100f * config.speedBonusPer100Kph;
    }

    private float GetPoliceBonus()
    {
        if (police == null || !police.IsChasing)
        {
            return 0f;
        }

        float closeness = 1f - Mathf.Clamp01(Mathf.Max(0f, police.GapToPlayer) / Mathf.Max(1f, config.policeBonusRange));
        return closeness * config.policeMaxBonus;
    }

    private void Award(string trick, float points)
    {
        float awarded = points * Multiplier;
        Score += awarded;
        TrickBonus = Mathf.Min(config.maxTrickBonus, TrickBonus + config.trickBonusPerTrick);
        TrickScored?.Invoke(trick, awarded);
    }

    private void UpdateDrift(float speedKph, float deltaTime)
    {
        bool sliding = player.IsGrounded && speedKph >= config.driftMinSpeedKph && Mathf.Abs(player.DriftAngle) >= config.driftMinAngle;
        if (sliding)
        {
            drifting = true;
            driftTime += deltaTime;
            driftGraceTimer = config.driftEndGrace;
            return;
        }

        if (!drifting)
        {
            return;
        }

        driftGraceTimer -= deltaTime;
        if (driftGraceTimer > 0f)
        {
            return;
        }

        if (driftTime >= config.driftMinDuration)
        {
            Award($"DRIFT {driftTime:0.0}s", config.driftBasePoints + config.driftPointsPerSecond * driftTime);
        }

        drifting = false;
        driftTime = 0f;
    }

    private void UpdateSpeedTricks(float speedKph, float deltaTime)
    {
        UpdateSpeedTrick(speedKph, deltaTime, config.bigSpeedKph, config.bigSpeedPoints, "BIG SPEED", ref bigSpeedTimer, ref bigSpeedArmed);
        UpdateSpeedTrick(speedKph, deltaTime, config.insaneSpeedKph, config.insaneSpeedPoints, "INSANE SPEED", ref insaneSpeedTimer, ref insaneSpeedArmed);
    }

    private void UpdateSpeedTrick(float speedKph, float deltaTime, float threshold, float points, string trick, ref float timer, ref bool armed)
    {
        if (speedKph < threshold - config.speedRearmDropKph)
        {
            armed = true;
        }

        timer = speedKph >= threshold ? timer + deltaTime : 0f;
        if (armed && timer >= config.speedHoldTime)
        {
            armed = false;
            Award(trick, points);
        }
    }

    // A pass happens when a car moves from ahead of the player to behind (along the player's
    // forward). Scores if the side gap between the bodies was tight and the player never touched it.
    private void UpdateTrafficPasses()
    {
        if (traffic == null)
        {
            return;
        }

        Transform playerTransform = player.transform;
        Vector3 playerVelocity = playerBody != null ? playerBody.linearVelocity : Vector3.zero;
        float minRelativeSpeed = config.nearMinRelativeSpeedKph / 3.6f;
        staleVehicles.Clear();
        staleVehicles.AddRange(lastAlongOffset.Keys);

        IReadOnlyList<TrafficVehicle> vehicles = traffic.Vehicles;
        for (int i = 0; i < vehicles.Count; i++)
        {
            TrafficVehicle vehicle = vehicles[i];
            staleVehicles.Remove(vehicle);
            Vector3 offset = vehicle.transform.position - playerTransform.position;
            float along = Vector3.Dot(offset, playerTransform.forward);
            bool hadPrevious = lastAlongOffset.TryGetValue(vehicle, out float previousAlong);
            lastAlongOffset[vehicle] = along;
            if (!hadPrevious || previousAlong <= 0f || along > 0f || vehicle.TouchedPlayer || offset.sqrMagnitude > 400f)
            {
                continue;
            }

            float sideGap = Mathf.Abs(Vector3.Dot(offset, playerTransform.right)) - PlayerHalfWidth - vehicle.transform.lossyScale.x * 0.5f;
            if (sideGap > config.nearDistance)
            {
                continue;
            }

            bool oncoming = Vector3.Dot(vehicle.transform.forward, playerTransform.forward) < 0f;
            if (oncoming)
            {
                if (player.SpeedMps >= minRelativeSpeed)
                {
                    Award("NEAR MISS", config.nearMissPoints);
                }

                continue;
            }

            // Overtaking: how much faster the player is going along the car's direction.
            float overtakeSpeed = Vector3.Dot(playerVelocity, vehicle.transform.forward) - vehicle.SpeedMps;
            if (overtakeSpeed >= minRelativeSpeed)
            {
                Award("NEAR PASS", config.nearPassPoints);
            }
        }

        foreach (TrafficVehicle gone in staleVehicles)
        {
            lastAlongOffset.Remove(gone);
        }
    }

    private void OnPlayerImpact(float speedChange, Vector3 pushDirection)
    {
        if (config == null || TrickBonus <= 0f)
        {
            return;
        }

        float before = TrickBonus;
        string label;
        if (speedChange >= config.majorImpactThreshold)
        {
            TrickBonus = 0f;
            label = "WRECKED";
        }
        else if (speedChange >= config.bigImpactThreshold)
        {
            TrickBonus = Mathf.Max(0f, TrickBonus - config.bigHitPenalty);
            label = "HIT";
        }
        else
        {
            TrickBonus = Mathf.Max(0f, TrickBonus - config.scrapePenalty);
            label = "SCRAPE";
        }

        MultiplierLost?.Invoke(label, before - TrickBonus);
    }
}
