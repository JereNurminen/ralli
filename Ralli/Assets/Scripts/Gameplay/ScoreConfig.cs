using UnityEngine;

[CreateAssetMenu(menuName = "Ralli/Gameplay/Score Config", fileName = "ScoreConfig")]
public class ScoreConfig : ScriptableObject
{
    [Header("Drift")]
    [Tooltip("Slide angle (degrees) that counts as drifting.")]
    public float driftMinAngle = 15f;
    public float driftMinSpeedKph = 30f;
    [Tooltip("Shortest drift (s) that scores.")]
    public float driftMinDuration = 0.7f;
    [Tooltip("A drift may dip under the angle this long (s) without ending.")]
    public float driftEndGrace = 0.3f;
    public float driftBasePoints = 100f;
    public float driftPointsPerSecond = 150f;

    [Header("Near pass / near miss")]
    [Tooltip("Side gap between the two bodies (m) that counts as tight.")]
    public float nearDistance = 1.5f;
    [Tooltip("Only count passes with at least this much speed difference (km/h).")]
    public float nearMinRelativeSpeedKph = 15f;
    [Tooltip("Passing a car going the same way.")]
    public float nearPassPoints = 250f;
    [Tooltip("Missing an oncoming car.")]
    public float nearMissPoints = 500f;

    [Header("Speed")]
    public float bigSpeedKph = 160f;
    public float bigSpeedPoints = 300f;
    public float insaneSpeedKph = 200f;
    public float insaneSpeedPoints = 800f;
    [Tooltip("How long the speed must be held (s).")]
    public float speedHoldTime = 1f;
    [Tooltip("Drop this far below a speed (km/h) before it can score again.")]
    public float speedRearmDropKph = 20f;

    [Header("Multiplier")]
    [Tooltip("Multiplier added per 100 km/h of current speed.")]
    public float speedBonusPer100Kph = 0.2f;
    [Tooltip("Multiplier added by every trick (kept until lost to hits).")]
    public float trickBonusPerTrick = 0.1f;
    public float maxTrickBonus = 5f;
    [Tooltip("Multiplier added with the police right on the player's bumper; fades to 0 at Police Bonus Range.")]
    public float policeMaxBonus = 2f;
    public float policeBonusRange = 100f;

    [Header("Hits (impact speed change, m/s)")]
    [Tooltip("Impacts below this are minor scrapes; below Major Impact they are big hits; above, major collisions.")]
    public float bigImpactThreshold = 3f;
    public float majorImpactThreshold = 8f;
    [Tooltip("Trick multiplier lost to a minor scrape and to a big hit. A major collision loses all of it.")]
    public float scrapePenalty = 0.2f;
    public float bigHitPenalty = 1f;
}
