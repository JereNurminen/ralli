using UnityEngine;

// Turns this object with the steering wheel, so hand grip anchors placed under it stay on the rim.
// It follows what the wheel visibly does (via its transform matrix, mirroring included) rather
// than its rotation value, which is ambiguous under the mirrored car model. Must be a child of the car.
[DefaultExecutionOrder(50)]
public class SteeringWheelFollower : MonoBehaviour
{
    [SerializeField] private Transform wheel;
    [Tooltip("The wheel's spin axis in its own local space.")]
    [SerializeField] private Vector3 wheelAxisLocal = Vector3.forward;

    private Vector3 axisInParent;
    private Vector3 restSpokeInParent;
    private Quaternion restLocalRotation;
    private bool bound;

    private void Update()
    {
        if (wheel == null || transform.parent == null)
        {
            return;
        }

        if (!bound)
        {
            axisInParent = GetInParent(wheelAxisLocal).normalized;
            restSpokeInParent = Vector3.ProjectOnPlane(GetInParent(GetSpokeLocal()), axisInParent);
            restLocalRotation = transform.localRotation;
            bound = true;
        }

        Vector3 spoke = Vector3.ProjectOnPlane(GetInParent(GetSpokeLocal()), axisInParent);
        float angle = Vector3.SignedAngle(restSpokeInParent, spoke, axisInParent);
        transform.localRotation = Quaternion.AngleAxis(angle, axisInParent) * restLocalRotation;
    }

    // Any local direction perpendicular to the spin axis works as a reference spoke.
    private Vector3 GetSpokeLocal()
    {
        return Vector3.Cross(wheelAxisLocal, Mathf.Abs(wheelAxisLocal.y) < 0.9f ? Vector3.up : Vector3.right);
    }

    private Vector3 GetInParent(Vector3 wheelLocalDirection)
    {
        return transform.parent.InverseTransformVector(wheel.TransformVector(wheelLocalDirection));
    }
}
