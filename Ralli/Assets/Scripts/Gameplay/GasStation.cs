using System;
using UnityEngine;

// A gas station beside the road, placed by StageDirector at a stage's start and finish. The prefab
// origin sits on the lot's road-side edge at road height, +Z along the road, the lot toward +X.
// Start Spot is where the car waits at the start station; Finish Zone (a trigger over the lot,
// sized by hand) ends the stage when the player's car drives into it.
public class GasStation : MonoBehaviour
{
    [SerializeField] private Transform startSpot;
    [SerializeField] private StationFinishZone finishZone;

    public Transform StartSpot => startSpot;

    public event Action PlayerArrived;

    public void SetAsFinish(bool isFinish)
    {
        finishZone.gameObject.SetActive(isFinish);
        finishZone.Entered -= OnPlayerArrived;
        if (isFinish)
        {
            finishZone.Entered += OnPlayerArrived;
        }
    }

    private void OnPlayerArrived()
    {
        PlayerArrived?.Invoke();
    }
}
