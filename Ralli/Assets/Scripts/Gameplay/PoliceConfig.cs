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

    [Header("Caught")]
    [Tooltip("Once the van has got past the player (caught), it brakes to a stop this hard (m/s²).")]
    public float caughtBrakeMps2 = 30f;

    [Header("Stage Finish")]
    [Tooltip("After the player finishes, the van drives on past the road's dead end and vanishes this far into the woods (m).")]
    public float passThroughVanishDistance = 60f;

    [Header("Traffic")]
    [Tooltip("Traffic cars within this distance of the van (m) are knocked loose so the van shoves them aside.")]
    public float shovePadding = 0.3f;

    [Header("Lights")]
    [Tooltip("Rotating beacon speed (turns per second). Red and blue spin half a turn apart.")]
    public float beaconTurnsPerSecond = 1.5f;
    [Tooltip("Brightness and reach (m) of the rotating beacon beams.")]
    public float beaconIntensity = 20f;
    public float beaconRange = 35f;
    [Tooltip("Width of each beacon beam (degrees).")]
    public float beaconBeamAngle = 40f;
    [Tooltip("Dim red/blue fill around the van between sweeps (fraction of beacon brightness).")]
    [Range(0f, 1f)] public float beaconFillAmount = 0.12f;
    [Tooltip("Glow of the roof light bar when a beam faces the viewer (HDR multiplier; bloom makes it flare).")]
    public float lightBarEmission = 16f;
    [ColorUsage(false)] public Color strobeRed = new Color(1f, 0.05f, 0.05f);
    [ColorUsage(false)] public Color strobeBlue = new Color(0.1f, 0.25f, 1f);
    [Tooltip("Van headlight brightness and reach (m).")]
    public float headlightIntensity = 12f;
    public float headlightRange = 70f;
    [Tooltip("How visible the fake light beams are (beacons and headlights). 0 = off.")]
    [Range(0f, 1f)] public float beamVisibility = 0.25f;

    [Header("Siren")]
    [Tooltip("Looping siren on the van, positional (with doppler). Empty = silent.")]
    public AudioClip siren;
    [Range(0f, 1f)] public float sirenVolume = 0.8f;
    [Tooltip("Distance (m) where the siren is at full volume, and where it stops fading.")]
    public float sirenMinDistance = 12f;
    public float sirenMaxDistance = 400f;
    [Range(0f, 5f)] public float sirenDoppler = 1f;
    [Tooltip("Fade-in when the van appears (s).")]
    public float sirenFadeInTime = 2f;

    [Header("Visuals")]
    [Tooltip("Headlight/tail light faces and flares on the van. Empty = none.")]
    public VehicleLightsConfig vehicleLights;
    [Tooltip("Van body size (x = width, y = height, z = length). Roughly a VW Transporter.")]
    public Vector3 vanSize = new Vector3(1.9f, 1.95f, 4.9f);
    public Color vanColor = Color.white;
}
