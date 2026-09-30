using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

// Stage music (see docs/2026-09-plan/06c-audio-clarified.md).
// Playback: Intro -> A -> B -> B -> ..., DSP-scheduled on two alternating sources so every
// section boundary is seamless. Gameplay changes the mix, never the arrangement:
// - Score multiplier -> volume boost (full at x1.5) and bass EQ boost (full at x2), re-evaluated a
//   few times per second and eased toward.
// - Impacts (pushed via OnImpact) -> a fast duck that recovers, independent of the rest.
// Final level = base + multiplier volume boost + impact duck (+ stage-finish fade).
public class MusicDirector : MonoBehaviour
{
    private const float SilentDb = -80f;

    [Header("Tracks")]
    [SerializeField] private MusicTrack[] tracks;
    [Tooltip("Start a stage with a random track when the scene starts.")]
    [SerializeField] private bool playOnStart = true;
    [Tooltip("Hold the music until the player turns the engine on, then start it after the delay below.")]
    [SerializeField] private bool waitForEngineStart = true;
    [SerializeField] private float musicStartDelay = 1f;

    [Header("Mixer")]
    [Tooltip("Music group of the mixer. Without it, volume falls back to the AudioSources and there is no bass EQ.")]
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private string volumeParameter = "MusicVolume";
    [SerializeField] private string bassGainParameter = "MusicBassGain";

    [Header("Levels")]
    [SerializeField] private float baseVolumeDb = -4f;
    [Tooltip("How quickly the volume boost and bass boost ease toward their targets (s).")]
    [SerializeField] private float mixSmoothTime = 0.8f;
    [Tooltip("How often gameplay state is checked (s).")]
    [SerializeField] private float evaluateInterval = 0.3f;

    [Header("Score multiplier -> Mix")]
    [Tooltip("Volume boost (dB) reached at the multiplier below; nothing at x1.")]
    [SerializeField] private float maxVolumeBoostDb = 2f;
    [SerializeField] private float volumeBoostFullMultiplier = 1.5f;
    [Tooltip("Bass EQ boost (dB) reached at the multiplier below; nothing at x1.")]
    [SerializeField] private float maxBassBoostDb = 3f;
    [SerializeField] private float bassBoostFullMultiplier = 2f;

    [Header("Impact Ducking")]
    [Tooltip("Impact speed change (m/s) where a crash counts as significant, and as major.")]
    [SerializeField] private Vector2 impactSeverityThresholds = new Vector2(5f, 12f);
    [SerializeField] private float significantDuckDb = -10f;
    [SerializeField] private float majorDuckDb = -20f;
    [Tooltip("Fade-in time of the duck (s). Short, but not instant, to avoid clicks.")]
    [SerializeField] private float duckAttackTime = 0.05f;
    [SerializeField] private float significantRecoverTime = 1.2f;
    [Tooltip("Silence held before a major crash starts recovering (s).")]
    [SerializeField] private float majorHoldTime = 0.5f;
    [SerializeField] private float majorRecoverTime = 3f;

    [Header("Stage Finish")]
    [SerializeField] private float finishFadeTime = 3f;

    private AudioSource[] sources;
    private int nextSourceIndex;
    private MusicTrack currentTrack;
    private int nextSection;
    private double nextSectionStartDsp;
    private bool playing;

    private CarController car;
    private ScoreSystem score;
    private float targetVolumeBoostDb;
    private float volumeBoostDb;
    private float volumeBoostVelocity;
    private float targetBassDb;
    private float bassDb;
    private float bassVelocity;

    private float duckDb;
    private float duckTargetDb;
    private float duckHoldTimer;
    private float duckRecoverRate;
    private float fadeDb;
    private float fadeRate;

    private void Awake()
    {
        sources = new AudioSource[2];
        for (int i = 0; i < sources.Length; i++)
        {
            var sourceObject = new GameObject($"MusicSource{i}");
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = musicGroup;
            sources[i] = source;
        }
    }

    private void Start()
    {
        car = FindFirstObjectByType<CarController>();
        score = FindFirstObjectByType<ScoreSystem>();
        if (car != null)
        {
            car.Impact += OnCarImpact;
        }

        StartCoroutine(EvaluateMixLoop());

        if (!playOnStart || tracks == null || tracks.Length == 0)
        {
            return;
        }

        if (waitForEngineStart && car != null && !car.EngineRunning)
        {
            car.EngineStarted += OnEngineStarted;
        }
        else
        {
            StartStage(tracks[Random.Range(0, tracks.Length)]);
        }
    }

    private void OnEngineStarted()
    {
        car.EngineStarted -= OnEngineStarted;
        StartCoroutine(StartRandomTrackAfter(musicStartDelay));
    }

    private IEnumerator StartRandomTrackAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartStage(tracks[Random.Range(0, tracks.Length)]);
    }

    private void OnDestroy()
    {
        if (car != null)
        {
            car.Impact -= OnCarImpact;
            car.EngineStarted -= OnEngineStarted;
        }
    }

    public void StartStage(MusicTrack track)
    {
        Begin(track, track != null && track.intro != null ? 0 : 1);
    }

    // Lower-energy restart after a breather stop: skip the intro.
    public void StartStageFromPartA(MusicTrack track)
    {
        Begin(track, 1);
    }

    public void FinishStage()
    {
        fadeRate = -SilentDb / Mathf.Max(0.01f, finishFadeTime);
    }

    // Severity = the impact's speed change (m/s).
    public void OnImpact(float severity)
    {
        float duck;
        float hold = 0f;
        float recoverTime;
        if (severity >= impactSeverityThresholds.y)
        {
            duck = majorDuckDb;
            hold = majorHoldTime;
            recoverTime = majorRecoverTime;
        }
        else if (severity >= impactSeverityThresholds.x)
        {
            duck = significantDuckDb;
            recoverTime = significantRecoverTime;
        }
        else
        {
            return;
        }

        if (duck < duckTargetDb || duckTargetDb >= 0f)
        {
            duckTargetDb = Mathf.Min(duck, duckTargetDb);
            duckHoldTimer = hold;
            duckRecoverRate = -duck / Mathf.Max(0.01f, recoverTime);
        }

    }

    private void OnCarImpact(float speedChange, Vector3 pushDirection)
    {
        OnImpact(speedChange);
    }

    private void Begin(MusicTrack track, int firstSection)
    {
        if (track == null)
        {
            return;
        }

        StopSources();
        currentTrack = track;
        nextSection = firstSection;
        nextSectionStartDsp = AudioSettings.dspTime + 0.1;
        fadeDb = 0f;
        fadeRate = 0f;
        playing = true;
        ScheduleNextSection();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        // Keep the next section queued a couple of seconds ahead of the DSP clock.
        if (playing && AudioSettings.dspTime > nextSectionStartDsp - 2.0)
        {
            ScheduleNextSection();
        }

        volumeBoostDb = Mathf.SmoothDamp(volumeBoostDb, targetVolumeBoostDb, ref volumeBoostVelocity, mixSmoothTime, Mathf.Infinity, deltaTime);
        bassDb = Mathf.SmoothDamp(bassDb, targetBassDb, ref bassVelocity, mixSmoothTime, Mathf.Infinity, deltaTime);
        UpdateDuck(deltaTime);
        UpdateFinishFade(deltaTime);
        ApplyMix();
    }

    // Intro (0) -> A (1) -> B (2) -> B ... each on the other source, starting exactly where the last ends.
    private void ScheduleNextSection()
    {
        AudioClip clip = GetSectionClip(nextSection);
        if (clip == null)
        {
            playing = false;
            return;
        }

        AudioSource source = sources[nextSourceIndex];
        source.clip = clip;
        source.PlayScheduled(nextSectionStartDsp);
        nextSourceIndex = 1 - nextSourceIndex;
        nextSectionStartDsp += (double)clip.samples / clip.frequency;
        nextSection = Mathf.Min(nextSection + 1, 2);
    }

    private AudioClip GetSectionClip(int section)
    {
        if (section == 0 && currentTrack.intro != null)
        {
            return currentTrack.intro;
        }

        if (section <= 1 && currentTrack.partA != null)
        {
            return currentTrack.partA;
        }

        return currentTrack.partB != null ? currentTrack.partB : currentTrack.partA;
    }

    private IEnumerator EvaluateMixLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.05f, evaluateInterval));
        while (true)
        {
            EvaluateMix();
            yield return wait;
        }
    }

    // The score multiplier drives the mix: x1 is the base mix, the volume boost is full by
    // volumeBoostFullMultiplier and the bass boost by bassBoostFullMultiplier.
    private void EvaluateMix()
    {
        float multiplier = score != null ? score.Multiplier : 1f;
        targetVolumeBoostDb = Mathf.InverseLerp(1f, volumeBoostFullMultiplier, multiplier) * maxVolumeBoostDb;
        targetBassDb = Mathf.InverseLerp(1f, bassBoostFullMultiplier, multiplier) * maxBassBoostDb;
    }

    private void UpdateDuck(float deltaTime)
    {
        if (duckTargetDb < 0f)
        {
            if (duckDb > duckTargetDb + 0.01f)
            {
                duckDb = Mathf.MoveTowards(duckDb, duckTargetDb, -duckTargetDb / Mathf.Max(0.001f, duckAttackTime) * deltaTime);
                return;
            }

            if (duckHoldTimer > 0f)
            {
                duckHoldTimer -= deltaTime;
                return;
            }

            duckTargetDb = 0f;
        }

        duckDb = Mathf.MoveTowards(duckDb, 0f, duckRecoverRate * deltaTime);
    }

    private void UpdateFinishFade(float deltaTime)
    {
        if (fadeRate <= 0f)
        {
            return;
        }

        fadeDb = Mathf.MoveTowards(fadeDb, SilentDb, fadeRate * deltaTime);
        if (fadeDb <= SilentDb + 0.01f)
        {
            StopSources();
            fadeRate = 0f;
        }
    }

    private void ApplyMix()
    {
        float volumeDb = Mathf.Max(SilentDb, baseVolumeDb + volumeBoostDb + duckDb + fadeDb);
        AudioMixer mixer = musicGroup != null ? musicGroup.audioMixer : null;
        if (mixer != null && mixer.SetFloat(volumeParameter, volumeDb))
        {
            // ParamEQ gain is a linear multiplier (1 = 0 dB).
            mixer.SetFloat(bassGainParameter, Mathf.Pow(10f, bassDb / 20f));
            return;
        }

        float linear = Mathf.Pow(10f, volumeDb / 20f);
        foreach (AudioSource source in sources)
        {
            source.volume = linear;
        }
    }

    private void StopSources()
    {
        playing = false;
        currentTrack = null;
        foreach (AudioSource source in sources)
        {
            source.Stop();
        }
    }
}
