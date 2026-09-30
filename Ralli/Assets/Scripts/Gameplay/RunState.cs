using UnityEngine;

// The current run, kept across the scene reload between stages.
public static class RunState
{
    private static bool started;

    public static int StageIndex { get; private set; }
    public static int RunSeed { get; private set; }
    // How far behind the police were (s) when the last stage finished.
    public static float LeadSeconds { get; private set; }
    public static float Score { get; private set; }

    // Seed of a stage's road (curves and elevation): the same run seed replays the same stages.
    public static int StageSeed => RunSeed + StageIndex * 7919;

    // Entering play mode starts from scratch, even with domain reload disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        started = false;
    }

    // Starts a run unless one is going. fixedSeed 0 = random run seed.
    public static void EnsureStarted(int fixedSeed)
    {
        if (!started)
        {
            StartNewRun(fixedSeed);
        }
    }

    public static void StartNewRun(int fixedSeed)
    {
        started = true;
        StageIndex = 0;
        RunSeed = fixedSeed != 0 ? fixedSeed : Random.Range(100000, 1000000);
        LeadSeconds = 0f;
        Score = 0f;
    }

    public static void CompleteStage(float leadSeconds, float score)
    {
        StageIndex++;
        LeadSeconds = leadSeconds;
        Score = score;
    }
}
