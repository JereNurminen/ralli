using UnityEngine;

// Look of the fake vehicle lights (glowing light faces + flares) shared by traffic and police.
[CreateAssetMenu(menuName = "Ralli/Vehicle/Vehicle Lights Config", fileName = "VehicleLightsConfig")]
public class VehicleLightsConfig : ScriptableObject
{
    [Header("Placement (m, relative to the body box)")]
    [Tooltip("Height of the lights above the bottom of the body.")]
    public float lightHeight = 0.6f;
    [Tooltip("Distance of each light from the body's side.")]
    public float sideInset = 0.3f;
    [Tooltip("Size of each headlight face (width, height).")]
    public Vector2 headlightSize = new Vector2(0.34f, 0.16f);
    [Tooltip("Size of each tail light face (width, height).")]
    public Vector2 tailLightSize = new Vector2(0.28f, 0.14f);

    [Header("Headlights")]
    [ColorUsage(false)] public Color headlightColor = new Color(1f, 0.94f, 0.82f);
    [Tooltip("Glow of the headlight faces (HDR multiplier; bloom flares it).")]
    public float headlightEmission = 6f;
    public float headlightFlareIntensity = 3f;
    [Tooltip("Flare size up close (m) and minimum on-screen size at distance.")]
    public float headlightFlareSize = 0.28f;
    public float headlightFlareAngularSize = 0.006f;

    [Header("Tail and brake lights")]
    [ColorUsage(false)] public Color tailLightColor = new Color(1f, 0.04f, 0.02f);
    public float tailLightEmission = 2.5f;
    [Tooltip("Glow of the tail lights while braking.")]
    public float brakeLightEmission = 8f;
    public float tailFlareIntensity = 0.8f;
    public float brakeFlareIntensity = 3f;
    public float tailFlareSize = 0.16f;
    public float tailFlareAngularSize = 0.004f;
}
