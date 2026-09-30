using UnityEngine;

// Two forward spot lights on the car. Built at runtime from these settings, so they can be tuned
// here without touching the car model.
public class CarHeadlights : MonoBehaviour
{
    [Tooltip("Headlight position relative to the car pivot (m). X is mirrored for the other side.")]
    [SerializeField] private Vector3 offset = new Vector3(0.6f, -0.35f, 1.9f);
    [Tooltip("Downward tilt (degrees).")]
    [SerializeField] private float pitch = 4f;
    [SerializeField] private Color color = new Color(1f, 0.93f, 0.8f);
    [SerializeField] private float intensity = 30f;
    [SerializeField] private float range = 70f;
    [SerializeField] private float spotAngle = 60f;
    [SerializeField] private float innerSpotAngle = 35f;
    [Tooltip("Only one headlight casts shadows, to keep the cost down.")]
    [SerializeField] private bool castShadows = true;
    [Tooltip("How visible the fake light beams are. 0 = off.")]
    [Range(0f, 1f)] [SerializeField] private float beamVisibility = 0.08f;
    [Tooltip("Beam length as a fraction of the range.")]
    [Range(0.1f, 1f)] [SerializeField] private float beamLength = 0.4f;

    private void Start()
    {
        CreateLight(-1f, castShadows);
        CreateLight(1f, false);
    }

    private void CreateLight(float side, bool shadows)
    {
        var lightObject = new GameObject(side < 0f ? "Headlight_L" : "Headlight_R");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(offset.x * side, offset.y, offset.z);
        lightObject.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        Light spot = lightObject.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = color;
        spot.intensity = intensity;
        spot.range = range;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = innerSpotAngle;
        spot.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        lightObject.AddComponent<LightCone>().Configure(beamVisibility, beamLength);
    }
}
