using UnityEngine;

[RequireComponent(typeof(Camera))]
public class InCarCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Eye Position")]
    [Tooltip("Driver eye offset from the car pivot in meters, car-local axes (ignores car scale). Left-hand drive = negative X.")]
    [SerializeField] private Vector3 eyeOffset = new Vector3(-0.37f, 0.2f, -0.15f);
    [Tooltip("Downward pitch in degrees.")]
    [SerializeField] private float pitchDegrees = 4f;

    [Header("Lens")]
    [Tooltip("Vertical field of view. Narrow and constant; no speed ramp.")]
    [SerializeField] private float fieldOfView = 55f;
    [SerializeField] private float nearClipPlane = 0.05f;

    private Camera cachedCamera;

    private void Start()
    {
        cachedCamera = GetComponent<Camera>();

        if (target == null)
        {
            CarController carController = FindFirstObjectByType<CarController>();
            if (carController != null)
            {
                target = carController.transform;
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.SetPositionAndRotation(
            target.position + target.rotation * eyeOffset,
            target.rotation * Quaternion.Euler(pitchDegrees, 0f, 0f)
        );
        cachedCamera.fieldOfView = fieldOfView;
        cachedCamera.nearClipPlane = nearClipPlane;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
