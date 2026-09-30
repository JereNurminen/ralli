using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Runs one stage of a run: a road from a gas station to the next (see
// docs/plans/2026-09-30-stage-stations-design.md). Before anything else starts it applies the
// stage's settings (runtime config copies), places the two stations and parks the car at the
// start one, engine off. Driving onto the finish lot stops the car, lets the police blast past and
// ends the stage; being caught ends the run. Either way a placeholder card follows, and a key
// press reloads the scene as the next stage (or a new run).
[DefaultExecutionOrder(-100)]
public class StageDirector : MonoBehaviour
{
    private enum Outcome { None, Finished, Caught }

    [SerializeField] private StageProgression progression;
    [SerializeField] private GasStation stationPrefab;

    [Header("Scene (found automatically when empty)")]
    [SerializeField] private RoadStreamGenerator road;
    [SerializeField] private CarController car;
    [SerializeField] private PoliceChaser police;
    [SerializeField] private TrafficStreamManager traffic;
    [SerializeField] private LightingDirector lighting;
    [SerializeField] private ScoreSystem score;
    [SerializeField] private MusicDirector music;

    [Header("Card")]
    [SerializeField] private float fadeTime = 1f;
    [Tooltip("Time to wait after being caught before the card (s).")]
    [SerializeField] private float caughtCardDelay = 2f;

    private GasStation finishStation;
    private Outcome outcome;
    private float leadSeconds;
    private float fade;
    private bool cardShown;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;

    private void Awake()
    {
        FindMissingReferences();
        RunState.EnsureStarted(progression.runSeed);
        StageProgression.StageSettings stage = progression.GetStage(RunState.StageIndex);

        RoadGenerationConfig roadConfig = Instantiate(road.Config);
        roadConfig.seed = RunState.StageSeed;
        roadConfig.roadWidth = stage.roadWidth;
        float startS = progression.runInLength;
        float finishS = startS + stage.stageLength;
        road.SetStage(roadConfig, new[] { startS, finishS }, finishS + progression.deadEndLength);

        if (lighting != null && stage.lightingPreset != null)
        {
            lighting.UsePreset(stage.lightingPreset);
        }

        if (traffic != null && traffic.Config != null)
        {
            TrafficConfig trafficConfig = Instantiate(traffic.Config);
            trafficConfig.vehiclesPerKilometer = stage.trafficPerKilometer;
            traffic.UseConfig(trafficConfig);
        }

        if (police != null && police.Config != null)
        {
            PoliceConfig policeConfig = Instantiate(police.Config);
            policeConfig.chaseSpeedKph = stage.policeChaseSpeedKph;
            if (RunState.StageIndex > 0)
            {
                policeConfig.startDelay = Mathf.Max(progression.minPoliceDelay, RunState.LeadSeconds);
            }

            police.UseConfig(policeConfig);
            police.CaughtPlayer += OnCaught;
        }
    }

    private void Start()
    {
        var lots = road.GetStationLots();
        for (int i = 0; i < lots.Count; i++)
        {
            RoadStreamGenerator.StationLot lot = lots[i];
            GasStation station = Instantiate(stationPrefab, lot.origin, Quaternion.LookRotation(lot.forward), transform);
            station.name = i == 0 ? "StartStation" : "FinishStation";
            bool isFinish = i == lots.Count - 1;
            station.SetAsFinish(isFinish);
            if (isFinish)
            {
                finishStation = station;
                station.PlayerArrived += OnFinished;
            }
            else if (i == 0)
            {
                ParkCar(station.StartSpot);
            }
        }
    }

    private void OnDestroy()
    {
        if (police != null)
        {
            police.CaughtPlayer -= OnCaught;
        }

        if (finishStation != null)
        {
            finishStation.PlayerArrived -= OnFinished;
        }
    }

    private void FindMissingReferences()
    {
        if (road == null) road = FindFirstObjectByType<RoadStreamGenerator>();
        if (car == null) car = FindFirstObjectByType<CarController>();
        if (police == null) police = FindFirstObjectByType<PoliceChaser>();
        if (traffic == null) traffic = FindFirstObjectByType<TrafficStreamManager>();
        if (lighting == null) lighting = FindFirstObjectByType<LightingDirector>();
        if (score == null) score = FindFirstObjectByType<ScoreSystem>();
        if (music == null) music = FindFirstObjectByType<MusicDirector>();
    }

    private void ParkCar(Transform spot)
    {
        var body = car.GetComponent<Rigidbody>();
        car.transform.SetPositionAndRotation(spot.position, spot.rotation);
        body.position = spot.position;
        body.rotation = spot.rotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void OnFinished()
    {
        if (outcome != Outcome.None)
        {
            return;
        }

        outcome = Outcome.Finished;
        car.BeginAssistedStop(progression.finishStopDeceleration);
        EndScoringAndMusic();
        if (police != null)
        {
            leadSeconds = police.GetLeadSeconds();
            police.PassThrough();
        }

        StartCoroutine(ShowCardAfter(progression.finishCardDelay));
    }

    private void OnCaught()
    {
        if (outcome != Outcome.None)
        {
            return;
        }

        outcome = Outcome.Caught;
        EndScoringAndMusic();
        StartCoroutine(ShowCardAfter(caughtCardDelay));
    }

    private void EndScoringAndMusic()
    {
        if (score != null)
        {
            score.enabled = false;
        }

        if (music != null)
        {
            music.FinishStage();
        }
    }

    private IEnumerator ShowCardAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        while (fade < 1f)
        {
            fade = Mathf.MoveTowards(fade, 1f, Time.deltaTime / Mathf.Max(0.01f, fadeTime));
            yield return null;
        }

        cardShown = true;
    }

    private void Update()
    {
        if (!cardShown || !ContinuePressed())
        {
            return;
        }

        if (outcome == Outcome.Finished)
        {
            RunState.CompleteStage(leadSeconds, score != null ? score.Score : RunState.Score);
        }
        else
        {
            RunState.StartNewRun(progression.runSeed);
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private static bool ContinuePressed()
    {
        return (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    // Placeholder stage card (the shop goes here later).
    private void OnGUI()
    {
        if (fade <= 0f)
        {
            return;
        }

        GUI.color = new Color(0f, 0f, 0f, fade);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        if (!cardShown)
        {
            return;
        }

        EnsureStyles();
        GUI.color = Color.white;
        float finalScore = score != null ? score.Score : RunState.Score;
        string title = outcome == Outcome.Finished ? $"STAGE {RunState.StageIndex + 1} COMPLETE" : "CAUGHT";
        string body = outcome == Outcome.Finished
            ? $"Lead {leadSeconds:0.0} s\nScore {finalScore:N0}\n\nPress Enter to continue"
            : $"Run over after stage {RunState.StageIndex + 1}\nScore {finalScore:N0}\n\nPress Enter to start a new run";
        float centerY = Screen.height * 0.5f;
        GUI.Label(new Rect(0f, centerY - 90f, Screen.width, 60f), title, titleStyle);
        GUI.Label(new Rect(0f, centerY - 20f, Screen.width, 160f), body, bodyStyle);
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 40, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = Color.white;
        bodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 22 };
        bodyStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
    }
}
