using UnityEngine;

// One time of day: sun, ambient light, fog, sky and a little post-processing. The sun position is
// given as a real compass direction (azimuth from north, clockwise) and height above the horizon;
// the player is assumed to head north on average, so the sun follows its real path through the day.
[CreateAssetMenu(menuName = "Ralli/Lighting/Lighting Preset", fileName = "LightingPreset")]
public class LightingPreset : ScriptableObject
{
    [Header("Sun (or moon)")]
    [Tooltip("Compass direction of the sun (degrees from north, clockwise: 90 = east, 180 = south, 270 = west).")]
    [Range(0f, 360f)] public float sunAzimuth = 180f;
    [Tooltip("Height of the sun above the horizon (degrees).")]
    [Range(-10f, 90f)] public float sunElevation = 35f;
    [ColorUsage(false)] public Color sunColor = Color.white;
    public float sunIntensity = 1.5f;
    [Range(0f, 1f)] public float shadowStrength = 0.9f;

    [Header("Ambient (sky / horizon / ground)")]
    [ColorUsage(false, true)] public Color ambientSky = new Color(0.5f, 0.55f, 0.65f);
    [ColorUsage(false, true)] public Color ambientEquator = new Color(0.4f, 0.4f, 0.4f);
    [ColorUsage(false, true)] public Color ambientGround = new Color(0.15f, 0.14f, 0.12f);

    [Header("Fog")]
    public bool fog = true;
    [ColorUsage(false)] public Color fogColor = new Color(0.6f, 0.65f, 0.7f);
    [Tooltip("Exponential-squared fog density. ~0.003 = hazy distance, ~0.012 = thick mist.")]
    public float fogDensity = 0.004f;

    [Header("Sky")]
    [Tooltip("Optional skybox material. Empty = plain sky color (usually matched to the fog).")]
    public Material skybox;
    [ColorUsage(false)] public Color skyColor = new Color(0.6f, 0.65f, 0.7f);

    [Header("Post-processing")]
    public float bloomIntensity = 0.6f;
    public float bloomThreshold = 1f;
    [Tooltip("Overall brightness adjustment (stops).")]
    public float postExposure;
}
