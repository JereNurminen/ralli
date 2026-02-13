using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.6f, -5.4f);
    [SerializeField] private float positionLerp = 10f;
    [SerializeField] private float rotationLerp = 10f;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float cameraRadius = 0.3f;
    [SerializeField] private float minimumGroundClearance = 1.0f;
    [SerializeField] private float lookAheadDistance = 7f;

    private void Start()
    {
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

        Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = transform.forward;
        }
        Quaternion yawRotation = Quaternion.LookRotation(flatForward, Vector3.up);

        Vector3 pivot = target.position + Vector3.up * 1.1f;
        Vector3 desiredPosition = pivot + yawRotation * offset;
        desiredPosition = ResolveObstacles(pivot, desiredPosition);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-positionLerp * Time.deltaTime));

        Vector3 lookPoint = target.position + flatForward * lookAheadDistance;
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
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
}
