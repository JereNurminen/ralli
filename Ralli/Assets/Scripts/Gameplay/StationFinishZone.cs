using System;
using UnityEngine;

// Trigger over a gas station lot; fires once when the player's car enters it.
[RequireComponent(typeof(Collider))]
public class StationFinishZone : MonoBehaviour
{
    private bool entered;

    public event Action Entered;

    private void OnTriggerEnter(Collider other)
    {
        if (entered || other.attachedRigidbody == null || !other.attachedRigidbody.TryGetComponent(out CarController _))
        {
            return;
        }

        entered = true;
        Entered?.Invoke();
    }
}
