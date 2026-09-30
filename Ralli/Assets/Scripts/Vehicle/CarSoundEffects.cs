using UnityEngine;
using UnityEngine.Audio;

// Tire screech and crash sounds. Screech loops while the car slides (drift angle) or skids on the
// handbrake, with a random screech clip picked each time it starts. Crashes play a random clip on
// impacts, louder for harder hits. Cabin sounds, so non-positional.
[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(CarInputReader))]
public class CarSoundEffects : MonoBehaviour
{
    private const string DefaultClipFolder = "Assets/ThirdParty/CascadiaGames_RacingSoundPack/";

    [Header("Clips")]
    [SerializeField] private AudioClip[] screechClips;
    [SerializeField] private AudioClip[] crashClips;
    [SerializeField] private AudioMixerGroup mixerGroup;

    [Header("Screech")]
    [Range(0f, 1f)] [SerializeField] private float screechVolume = 0.5f;
    [Tooltip("Drift angle (degrees) where the screech starts, and where it is full.")]
    [SerializeField] private Vector2 screechAngles = new Vector2(10f, 30f);
    [Tooltip("Speed (km/h) where the screech starts, and where it is full.")]
    [SerializeField] private Vector2 screechSpeedsKph = new Vector2(20f, 60f);
    [Tooltip("Screech amount while skidding on the handbrake, before the car slides.")]
    [Range(0f, 1f)] [SerializeField] private float handbrakeScreech = 0.6f;
    [SerializeField] private float screechFadeInTime = 0.08f;
    [SerializeField] private float screechFadeOutTime = 0.25f;
    [Tooltip("Pitch at the start of the speed range, and at full.")]
    [SerializeField] private Vector2 screechPitch = new Vector2(0.9f, 1.05f);

    [Header("Crash")]
    [Range(0f, 1f)] [SerializeField] private float crashVolume = 1f;
    [Tooltip("Impact speed change (m/s) of the quietest crash sound, and of a full-volume one.")]
    [SerializeField] private Vector2 crashSeverity = new Vector2(1.5f, 8f);
    [Range(0f, 1f)] [SerializeField] private float quietestCrashVolume = 0.25f;
    [Tooltip("Shortest gap between crash sounds (s), so one hit's contacts don't stack.")]
    [SerializeField] private float crashCooldown = 0.15f;

    private CarController car;
    private CarInputReader input;
    private AudioSource screechSource;
    private AudioSource crashSource;
    private float screechLevel;
    private float lastCrashTime = float.NegativeInfinity;

    private void Start()
    {
        car = GetComponent<CarController>();
        input = GetComponent<CarInputReader>();

        var root = new GameObject("CarSoundEffects");
        root.transform.SetParent(transform, false);
        screechSource = CreateSource(root);
        screechSource.loop = true;
        crashSource = CreateSource(root);

        car.Impact += OnImpact;
    }

    private void OnDestroy()
    {
        if (car != null)
        {
            car.Impact -= OnImpact;
        }
    }

    private void Update()
    {
        float speedKph = car.SpeedMps * 3.6f;
        float speed = Mathf.InverseLerp(screechSpeedsKph.x, screechSpeedsKph.y, speedKph);
        float slide = Mathf.InverseLerp(screechAngles.x, screechAngles.y, Mathf.Abs(car.DriftAngle));
        if (input.Handbrake)
        {
            slide = Mathf.Max(slide, handbrakeScreech);
        }

        float target = car.IsGrounded ? slide * speed : 0f;
        float fadeTime = target > screechLevel ? screechFadeInTime : screechFadeOutTime;
        screechLevel = Mathf.MoveTowards(screechLevel, target, Time.deltaTime / Mathf.Max(0.01f, fadeTime));
        UpdateScreechSource(speed);
    }

    private void UpdateScreechSource(float speed)
    {
        if (screechLevel <= 0.001f)
        {
            if (screechSource.isPlaying)
            {
                screechSource.Stop();
            }

            return;
        }

        if (!screechSource.isPlaying)
        {
            AudioClip clip = PickClip(screechClips);
            if (clip == null)
            {
                return;
            }

            screechSource.clip = clip;
            screechSource.Play();
        }

        screechSource.volume = screechLevel * screechVolume;
        screechSource.pitch = Mathf.Lerp(screechPitch.x, screechPitch.y, speed);
    }

    private void OnImpact(float speedChange, Vector3 pushDirection)
    {
        if (speedChange < crashSeverity.x || Time.time - lastCrashTime < crashCooldown)
        {
            return;
        }

        AudioClip clip = PickClip(crashClips);
        if (clip == null)
        {
            return;
        }

        lastCrashTime = Time.time;
        float severity = Mathf.InverseLerp(crashSeverity.x, crashSeverity.y, speedChange);
        crashSource.pitch = Random.Range(0.92f, 1.08f);
        crashSource.PlayOneShot(clip, crashVolume * Mathf.Lerp(quietestCrashVolume, 1f, severity));
    }

    private AudioSource CreateSource(GameObject root)
    {
        AudioSource source = root.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = mixerGroup;
        return source;
    }

    private static AudioClip PickClip(AudioClip[] clips)
    {
        return clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;
    }

#if UNITY_EDITOR
    // Fills in the Cascadia Games racing pack clips when the component is added.
    private void Reset()
    {
        screechClips = new[] { LoadClip("screech1"), LoadClip("screech2") };
        crashClips = new[] { LoadClip("crash1"), LoadClip("crash2") };
    }

    private static AudioClip LoadClip(string name)
    {
        return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultClipFolder + name + ".ogg");
    }
#endif
}
