using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Makes the police impossible to miss: the screen-edge vignette flashes red/blue in time with the
// van's strobe, growing stronger as the van closes in. Works on a runtime copy of the volume
// profile, so the profile asset is never changed.
[RequireComponent(typeof(Volume))]
public class PoliceScreenFlash : MonoBehaviour
{
    [SerializeField] private PoliceChaser police;
    [Tooltip("The flash starts when the van is this close (m) and is full strength at the player's bumper.")]
    [SerializeField] private float startDistance = 80f;
    [Tooltip("Extra vignette intensity at full strength.")]
    [Range(0f, 1f)] [SerializeField] private float maxIntensityBoost = 0.3f;
    [Tooltip("How far the vignette color is pulled toward red/blue at full strength.")]
    [Range(0f, 1f)] [SerializeField] private float colorStrength = 0.85f;
    [Tooltip("How quickly each flash fades in and out (s).")]
    [SerializeField] private float flashFadeTime = 0.06f;

    private Vignette vignette;
    private Color baseColor;
    private float baseIntensity;
    private float amount;

    private void Start()
    {
        if (police == null)
        {
            police = FindFirstObjectByType<PoliceChaser>();
        }

        if (GetComponent<Volume>().profile.TryGet(out vignette))
        {
            vignette.active = true;
            vignette.color.overrideState = true;
            vignette.intensity.overrideState = true;
            baseColor = vignette.color.value;
            baseIntensity = vignette.intensity.value;
        }
    }

    private void LateUpdate()
    {
        if (vignette == null)
        {
            return;
        }

        PoliceLights lights = police != null ? police.Lights : null;
        float target = 0f;
        Color flashColor = baseColor;
        if (lights != null)
        {
            float proximity = 1f - Mathf.Clamp01(Mathf.Abs(police.GapToPlayer) / Mathf.Max(1f, startDistance));
            target = proximity * lights.FlashAmount;
            flashColor = lights.FlashColor;
        }

        amount = Mathf.MoveTowards(amount, target, Time.deltaTime / Mathf.Max(0.001f, flashFadeTime));
        vignette.color.value = Color.Lerp(baseColor, flashColor, amount * colorStrength);
        vignette.intensity.value = Mathf.Clamp01(baseIntensity + maxIntensityBoost * amount);
    }
}
