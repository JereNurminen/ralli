using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Applies a LightingPreset: sun direction/color, ambient, fog, sky, and bloom/exposure. Runs in the
// editor too, so changing Active Preset (or a preset's values) shows up immediately. Volume
// changes are play-mode only and go to a runtime copy of the profile, never the asset.
[ExecuteAlways]
public class LightingDirector : MonoBehaviour
{
    [SerializeField] private LightingPreset[] presets;
    [SerializeField] private int activePreset;
    [SerializeField] private Light sun;
    [SerializeField] private Volume volume;
    [Tooltip("World yaw of north (degrees). The road starts heading along +Z, which is treated as north.")]
    [SerializeField] private float northYaw;
    [Tooltip("Tone mapping style. Neutral keeps colors; ACES is filmic but desaturates and darkens.")]
    [SerializeField] private TonemappingMode tonemapping = TonemappingMode.Neutral;
    [Tooltip("How lights (headlights, beacons) fade with distance on road, ground and foliage. 1 = physical; lower = reach farther without burning out up close.")]
    [Range(0.3f, 1f)] [SerializeField] private float lightFalloff = 0.6f;

    private static readonly int LightFalloffId = Shader.PropertyToID("_RalliLightFalloff");

    public LightingPreset ActivePreset => presets != null && presets.Length > 0
        ? presets[Mathf.Clamp(activePreset, 0, presets.Length - 1)]
        : null;

    public void SetPreset(int index)
    {
        activePreset = index;
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    public void Apply()
    {
        LightingPreset preset = ActivePreset;
        if (preset == null)
        {
            return;
        }

        if (sun != null)
        {
            // A directional light shines along its forward, i.e. away from the sun.
            sun.transform.rotation = Quaternion.Euler(preset.sunElevation, northYaw + preset.sunAzimuth + 180f, 0f);
            sun.color = preset.sunColor;
            sun.intensity = preset.sunIntensity;
            sun.shadowStrength = preset.shadowStrength;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = preset.ambientSky;
        RenderSettings.ambientEquatorColor = preset.ambientEquator;
        RenderSettings.ambientGroundColor = preset.ambientGround;
        Shader.SetGlobalFloat(LightFalloffId, lightFalloff);

        // Fog will come from a weather system; the time of day never adds it.
        RenderSettings.fog = false;
        RenderSettings.skybox = preset.skybox;
        DynamicGI.UpdateEnvironment();

        foreach (Camera sceneCamera in Camera.allCameras)
        {
            ApplySky(sceneCamera, preset);
        }

        if (Application.isPlaying && volume != null)
        {
            ApplyPostProcessing(volume.profile, preset, tonemapping);
        }
    }

    public static void ApplySky(Camera target, LightingPreset preset)
    {
        target.clearFlags = preset.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        target.backgroundColor = preset.skyColor;
    }

    private static void ApplyPostProcessing(VolumeProfile profile, LightingPreset preset, TonemappingMode tonemappingMode)
    {
        if (profile.TryGet(out Tonemapping tonemapper))
        {
            tonemapper.mode.Override(tonemappingMode);
        }

        if (profile.TryGet(out Bloom bloom))
        {
            bloom.intensity.Override(preset.bloomIntensity);
            bloom.threshold.Override(preset.bloomThreshold);
        }

        if (!profile.TryGet(out ColorAdjustments colorAdjustments))
        {
            colorAdjustments = profile.Add<ColorAdjustments>();
        }

        colorAdjustments.postExposure.Override(preset.postExposure);
        colorAdjustments.saturation.Override(preset.saturation);
        colorAdjustments.contrast.Override(preset.contrast);
    }
}
