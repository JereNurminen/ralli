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
    [Tooltip("Sustained pitch lift while Overdrive is in.")]
    [SerializeField] private float overdrivePitchBoost = 0.1f;
    [Tooltip("Sustained volume lift while Overdrive is in.")]
    [SerializeField] private float overdriveVolumeBoost = 0.3f;
    [Tooltip("Extra volume swell the moment Overdrive kicks in.")]
    [SerializeField] private float engagePunchVolume = 0.5f;
    [Tooltip("Extra pitch swell the moment Overdrive kicks in.")]
    [SerializeField] private float engagePunchPitch = 0.08f;
    [Tooltip("Seconds for the engage swell to fade out.")]
    [SerializeField] private float engagePunchTime = 0.35f;

    private CarController car;
    private CarInputReader input;
    private float throttleBlend;
    private float fadeIn;
    private float punch;
    private bool wasOverdriving;
    private AudioSource startupSource;

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

        startupSource = root.AddComponent<AudioSource>();
        startupSource.outputAudioMixerGroup = mixerGroup;
        startupSource.spatialBlend = 0f;
        startupSource.playOnAwake = false;

        // Silent until the engine is turned on.
        if (car.EngineRunning)
        {
            PlayStartup();
        }
        else
        {
            car.EngineStarted += PlayStartup;
        }
    }

    private void OnDestroy()
    {
        if (car != null)
        {
            car.EngineStarted -= PlayStartup;
        }
    }

    private void PlayStartup()
    {
        if (startup != null)
        {
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
        fadeIn = Mathf.MoveTowards(fadeIn, car.EngineRunning ? 1f : 0f, deltaTime / Mathf.Max(0.01f, fadeInTime));
        throttleBlend = Mathf.MoveTowards(throttleBlend, input.Throttle, deltaTime / Mathf.Max(0.01f, throttleBlendTime));

        float rpm = car.EngineRpm01;
        float overdrive = car.OverdriveFactor;
        UpdateEngagePunch(deltaTime);
        float master = volume * fadeIn * (1f + overdriveVolumeBoost * overdrive + engagePunchVolume * punch);
        float pitchLift = 1f + overdrivePitchBoost * overdrive + engagePunchPitch * punch;
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
            pitch *= pitchLift;

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

    // Swell that snaps to full when Overdrive kicks in and fades out quickly (eased).
    private void UpdateEngagePunch(float deltaTime)
    {
        bool overdriving = input.Overdrive && input.Throttle > 0.5f;
        if (overdriving && !wasOverdriving)
        {
            punch = 1f;
        }

        wasOverdriving = overdriving;
        punch = Mathf.MoveTowards(punch, 0f, deltaTime / Mathf.Max(0.01f, engagePunchTime));
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
