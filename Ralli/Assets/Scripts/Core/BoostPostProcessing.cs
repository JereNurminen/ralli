using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Volume))]
public class BoostPostProcessing : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private CarController carController;

    [Header("Motion Blur")]
    [SerializeField] private float maxMotionBlurIntensity = 0.35f;

    [Header("Chromatic Aberration")]
    [SerializeField] private float maxChromaticAberration = 0.12f;

    [Header("Smoothing")]
    [SerializeField] private float rampUp = 5f;
    [SerializeField] private float rampDown = 3f;

    private Volume volume;
    private MotionBlur motionBlur;
    private ChromaticAberration chromaticAberration;
    private float smoothedBoost;

    private void Start()
    {
        volume = GetComponent<Volume>();

        if (carController == null)
            carController = FindFirstObjectByType<CarController>();

        if (volume.profile.TryGet(out motionBlur))
            motionBlur.intensity.overrideState = true;

        if (volume.profile.TryGet(out chromaticAberration))
            chromaticAberration.intensity.overrideState = true;
    }

    private void LateUpdate()
    {
        float target = carController != null ? carController.BoostFactor : 0f;
        float rate = target > smoothedBoost ? rampUp : rampDown;
        smoothedBoost = Mathf.Lerp(smoothedBoost, target, 1f - Mathf.Exp(-rate * Time.deltaTime));

        if (motionBlur != null)
            motionBlur.intensity.value = smoothedBoost * maxMotionBlurIntensity;

        if (chromaticAberration != null)
            chromaticAberration.intensity.value = smoothedBoost * maxChromaticAberration;
    }
}
