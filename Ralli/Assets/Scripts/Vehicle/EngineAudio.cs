using UnityEngine;
using UnityEngine.Audio;

// Prototype engine sound: crossfades looping clips recorded at different RPMs by the car's fake
// RPM, pitching each only a little around its recorded point, and blends on/off-throttle
// variants. Overdrive adds a small pitch and volume lift. Cabin sound, so non-positional.
[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(CarInputReader))]
public class EngineAudio : MonoBehaviour
{
    [System.Serializable]
    private class RpmLayer
    {
        [Tooltip("RPM this clip was recorded at, on the car's 0 (off) to 1 (limiter) scale.")]
        [Range(0f, 1f)] public float rpm01;
        public AudioClip onThrottle;
        [Tooltip("Optional. Falls back to the on-throttle clip.")]
        public AudioClip offThrottle;
        [HideInInspector] public AudioSource onSource;
        [HideInInspector] public AudioSource offSource;
    }

    private const string DefaultClipFolder = "Assets/ThirdParty/Car Engine Sound - i6 German Free/Assets/Audio/i6_german_free/Interior/";

    [Header("Clips")]
    [SerializeField] private AudioClip startup;
    [Tooltip("Engine loops, lowest RPM first.")]
    [SerializeField] private RpmLayer[] layers =
    {
        new RpmLayer { rpm01 = 0.12f },
        new RpmLayer { rpm01 = 0.3f },
        new RpmLayer { rpm01 = 0.55f },
        new RpmLayer { rpm01 = 0.8f },
        new RpmLayer { rpm01 = 1f }
    };

    [Header("Mix")]
    [SerializeField] private AudioMixerGroup mixerGroup;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;
    [Tooltip("Off-throttle loops relative to on-throttle.")]
    [Range(0f, 1f)] [SerializeField] private float offThrottleVolume = 0.7f;
    [Tooltip("Max pitch change from a clip's recorded RPM (0.2 = ±20%).")]
    [SerializeField] private float maxPitchShift = 0.2f;
    [SerializeField] private float throttleBlendTime = 0.15f;
    [SerializeField] private float fadeInTime = 0.8f;

    [Header("Overdrive")]
    [SerializeField] private float overdrivePitchLift = 0.05f;
    [SerializeField] private float overdriveVolumeLift = 0.15f;

    private CarController car;
    private CarInputReader input;
    private float throttleBlend;
    private float fadeIn;

    private void Start()
    {
        car = GetComponent<CarController>();
        input = GetComponent<CarInputReader>();

        var root = new GameObject("EngineAudio");
        root.transform.SetParent(transform, false);
        foreach (RpmLayer layer in layers)
        {
            layer.onSource = CreateLoop(root, layer.onThrottle);
            layer.offSource = layer.offThrottle != null ? CreateLoop(root, layer.offThrottle) : null;
        }

        if (startup != null)
        {
            AudioSource startupSource = root.AddComponent<AudioSource>();
            startupSource.outputAudioMixerGroup = mixerGroup;
            startupSource.spatialBlend = 0f;
            startupSource.PlayOneShot(startup, volume);
        }
    }

    private void Update()
    {
        if (car == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        fadeIn = Mathf.MoveTowards(fadeIn, 1f, deltaTime / Mathf.Max(0.01f, fadeInTime));
        throttleBlend = Mathf.MoveTowards(throttleBlend, input.Throttle, deltaTime / Mathf.Max(0.01f, throttleBlendTime));

        float rpm = car.EngineRpm01;
        float overdrive = car.OverdriveFactor;
        float master = volume * fadeIn * (1f + overdriveVolumeLift * overdrive);
        float onGain = Mathf.Sin(throttleBlend * Mathf.PI * 0.5f);
        float offGain = Mathf.Cos(throttleBlend * Mathf.PI * 0.5f) * offThrottleVolume;

        for (int i = 0; i < layers.Length; i++)
        {
            RpmLayer layer = layers[i];
            if (layer.onSource == null)
            {
                continue;
            }

            float weight = GetLayerWeight(i, rpm) * master;
            float pitch = Mathf.Clamp(rpm / Mathf.Max(0.01f, layer.rpm01), 1f - maxPitchShift, 1f + maxPitchShift);
            pitch *= 1f + overdrivePitchLift * overdrive;

            if (layer.offSource != null)
            {
                SetLoop(layer.onSource, weight * onGain, pitch);
                SetLoop(layer.offSource, weight * offGain, pitch);
            }
            else
            {
                SetLoop(layer.onSource, weight * (onGain + offGain), pitch);
            }
        }
    }

    // Equal-power crossfade between the two layers either side of the current RPM.
    private float GetLayerWeight(int index, float rpm)
    {
        int last = layers.Length - 1;
        if (rpm <= layers[0].rpm01)
        {
            return index == 0 ? 1f : 0f;
        }

        if (rpm >= layers[last].rpm01)
        {
            return index == last ? 1f : 0f;
        }

        for (int i = 0; i < last; i++)
        {
            if (rpm < layers[i + 1].rpm01)
            {
                float t = Mathf.InverseLerp(layers[i].rpm01, layers[i + 1].rpm01, rpm);
                if (index == i)
                {
                    return Mathf.Cos(t * Mathf.PI * 0.5f);
                }

                return index == i + 1 ? Mathf.Sin(t * Mathf.PI * 0.5f) : 0f;
            }
        }

        return 0f;
    }

    private AudioSource CreateLoop(GameObject root, AudioClip clip)
    {
        if (clip == null)
        {
            return null;
        }

        AudioSource source = root.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.outputAudioMixerGroup = mixerGroup;
        source.time = Random.Range(0f, clip.length);
        source.Play();
        return source;
    }

    private static void SetLoop(AudioSource source, float loopVolume, float pitch)
    {
        source.volume = loopVolume;
        source.pitch = pitch;
    }

#if UNITY_EDITOR
    // Fills in the i6 German interior clips when the component is added.
    private void Reset()
    {
        startup = LoadClip("int_startup");
        string[] names = { "int_idle", "int_low", "int_med", "int_high", "int_maxRPM" };
        for (int i = 0; i < layers.Length && i < names.Length; i++)
        {
            bool single = names[i] == "int_idle" || names[i] == "int_maxRPM";
            layers[i].onThrottle = LoadClip(single ? names[i] : names[i] + "_on");
            layers[i].offThrottle = single ? null : LoadClip(names[i] + "_off");
        }
    }

    private static AudioClip LoadClip(string name)
    {
        return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultClipFolder + name + ".wav");
    }
#endif
}
