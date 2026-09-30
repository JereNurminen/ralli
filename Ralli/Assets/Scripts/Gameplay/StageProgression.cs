using System;
using UnityEngine;

// Per-stage settings, by how far into the run the player is. Stage i uses entry i; stages past
// the end of the list reuse the last entry.
[CreateAssetMenu(menuName = "Ralli/Gameplay/Stage Progression", fileName = "StageProgression")]
public class StageProgression : ScriptableObject
{
    [Serializable]
    public class StageSettings
    {
        [Tooltip("Road length from the start station to the finish station (m).")]
        public float stageLength = 3000f;
        public LightingPreset lightingPreset;
        public float roadWidth = 8f;
        [Tooltip("Traffic cars per kilometer.")]
        public float trafficPerKilometer = 10f;
        public float policeChaseSpeedKph = 140f;
    }

    [Tooltip("Run seed for the stage roads. 0 = random every run.")]
    public int runSeed;

    [Header("Layout")]
    [Tooltip("Road from its start (dead end in the woods, where the police appear) to the start station (m).")]
    public float runInLength = 200f;
    [Tooltip("Road past the finish station before it dead-ends in the woods (m).")]
    public float deadEndLength = 150f;

    [Header("Police")]
    [Tooltip("Shortest police start delay a stage can get from the previous stage's lead (s).")]
    public float minPoliceDelay = 3f;

    [Header("Finish")]
    [Tooltip("How hard the car is braked to a stop on reaching the finish lot (m/s²).")]
    public float finishStopDeceleration = 25f;
    [Tooltip("Time between finishing and the stage card (s).")]
    public float finishCardDelay = 3f;

    public StageSettings[] stages = { new StageSettings() };

    public StageSettings GetStage(int index)
    {
        return stages[Mathf.Clamp(index, 0, stages.Length - 1)];
    }
}
