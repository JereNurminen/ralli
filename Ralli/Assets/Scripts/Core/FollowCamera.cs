using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.6f, -5.4f);
    [SerializeField] private float nearDistanceScale = 0.65f;
    [SerializeField] private float farDistanceScale = 1.25f;
    [SerializeField] private float speedForMaxDistance = 36f;
    [SerializeField] private float speedResponse = 6f;
    [SerializeField] private float positionLerp = 10f;
    [SerializeField] private float rotationLerp = 10f;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float cameraRadius = 0.3f;
    [SerializeField] private float minimumGroundClearance = 1.0f;
    [SerializeField] private float nearLookAheadDistance = 4.5f;
    [SerializeField] private float farLookAheadDistance = 11f;
    [SerializeField] private float nearFieldOfView = 60f;
    [SerializeField] private float farFieldOfView = 76f;
    [SerializeField] private float fovLerp = 6f;

    private CarController targetCarController;
    private Rigidbody targetRigidbody;
    private Camera cachedCamera;
    private float smoothedSpeedMps;

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

        CacheTargetMotionSources();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = transform.forward;
        }
        Quaternion yawRotation = Quaternion.LookRotation(flatForward, Vector3.up);
        float speedMps = GetTargetSpeedMps();
        float speedBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, speedResponse) * Time.deltaTime);
        smoothedSpeedMps = Mathf.Lerp(smoothedSpeedMps, speedMps, speedBlend);
        float speedT = Mathf.Clamp01(smoothedSpeedMps / Mathf.Max(0.1f, speedForMaxDistance));
        float distanceScale = Mathf.Lerp(nearDistanceScale, farDistanceScale, speedT);

        Vector3 pivot = target.position + Vector3.up * 1.1f;
        Vector3 desiredPosition = pivot + yawRotation * (offset * distanceScale);
        desiredPosition = ResolveObstacles(pivot, desiredPosition);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-positionLerp * Time.deltaTime));

        float lookAheadDistance = Mathf.Lerp(nearLookAheadDistance, farLookAheadDistance, speedT);
        Vector3 lookPoint = target.position + flatForward * lookAheadDistance;
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationLerp * Time.deltaTime));

        if (cachedCamera != null)
        {
            float targetFov = Mathf.Lerp(nearFieldOfView, farFieldOfView, speedT);
            cachedCamera.fieldOfView = Mathf.Lerp(
                cachedCamera.fieldOfView,
                targetFov,
                1f - Mathf.Exp(-Mathf.Max(0.01f, fovLerp) * Time.deltaTime)
            );
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        CacheTargetMotionSources();
    }

    private Vector3 ResolveObstacles(Vector3 pivot, Vector3 desiredPosition)
    {
        Vector3 ray = desiredPosition - pivot;
        float distance = ray.magnitude;
        if (distance <= 0.001f)
        {
            return desiredPosition;
        }

        Vector3 direction = ray / distance;
        if (Physics.SphereCast(pivot, cameraRadius, direction, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            desiredPosition = hit.point - direction * (cameraRadius + 0.1f);
        }

        float minY = target.position.y - 0.5f + minimumGroundClearance;
        if (desiredPosition.y < minY)
        {
            desiredPosition.y = minY;
        }

        return desiredPosition;
    }

    private void CacheTargetMotionSources()
    {
        targetCarController = null;
        targetRigidbody = null;

        if (target == null)
        {
            return;
        }

        targetCarController = target.GetComponent<CarController>();
        if (targetCarController == null)
        {
            targetCarController = target.GetComponentInParent<CarController>();
        }

        targetRigidbody = target.GetComponent<Rigidbody>();
        if (targetRigidbody == null)
        {
            targetRigidbody = target.GetComponentInParent<Rigidbody>();
        }
    }

    private float GetTargetSpeedMps()
    {
        if (targetCarController != null)
        {
            return Mathf.Abs(targetCarController.SpeedMps);
        }

        if (targetRigidbody != null)
        {
            return targetRigidbody.linearVelocity.magnitude;
        }

        CacheTargetMotionSources();

        if (targetCarController != null)
        {
            return Mathf.Abs(targetCarController.SpeedMps);
        }

        if (targetRigidbody != null)
        {
            return targetRigidbody.linearVelocity.magnitude;
        }

        return 0f;
    }
}
