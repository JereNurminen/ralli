using UnityEngine;

[CreateAssetMenu(menuName = "Ralli/Gameplay/Police Config", fileName = "PoliceConfig")]
public class PoliceConfig : ScriptableObject
{
    [Header("Start")]
    [Tooltip("Seconds after the scene starts before the police appear.")]
    public float startDelay = 10f;
    [Tooltip("How far behind the player (along the road, m) the police appear.")]
    public float spawnDistanceBehind = 200f;

    [Header("Chase")]
    [Tooltip("Constant chase speed (km/h).")]
    public float chaseSpeedKph = 140f;
    [Tooltip("How quickly the van gets up to chase speed (m/s²).")]
    public float accelerationMps2 = 5f;
    [Tooltip("Within this distance of the player (m), the van steers onto the player's line.")]
    public float homingDistance = 40f;
    [Tooltip("Sideways speed when changing line (m/s).")]
    public float lateralSpeed = 3f;

    [Header("Ramming (repeats: chase, ram, back off)")]
    [Tooltip("Start a ram when this close behind the player (m, bumper to bumper).")]
    public float ramTriggerDistance = 12f;
    [Tooltip("How much faster than the player the van drives into them (km/h).")]
    public float ramClosingSpeedKph = 25f;
    [Tooltip("Gap (m) that counts as contact.")]
    public float ramContactGap = 0.3f;
    [Tooltip("Extra forward shove given to the player on a hit (m/s), on top of the physical contact.")]
    public float ramPushSpeed = 3f;
    [Tooltip("Random sideways jolt on a hit, up to this much either way (m/s).")]
    public float ramSideKick = 2.5f;
    [Tooltip("Small upward hop on a hit (m/s).")]
    public float ramUpKick = 0.4f;
    [Tooltip("Random spin on a hit, up to this much either way (degrees per second).")]
    public float ramYawKick = 50f;
    [Tooltip("After a hit, the van hangs back this long before chasing again (s).")]
    public float backoffTime = 1f;
    [Tooltip("While backing off, the van slows to this fraction of the player's speed.")]
    [Range(0f, 1f)] public float backoffSpeedFactor = 0.5f;
    [Tooltip("Braking while backing off (m/s²).")]
    public float backoffBrakeMps2 = 12f;

    [Header("Traffic")]
    [Tooltip("Traffic cars within this distance of the van (m) are knocked loose so the van shoves them aside.")]
    public float shovePadding = 0.3f;

    [Header("Visuals")]
    [Tooltip("Van body size (x = width, y = height, z = length). Roughly a VW Transporter.")]
    public Vector3 vanSize = new Vector3(1.9f, 1.95f, 4.9f);
    public Color vanColor = Color.white;
}
