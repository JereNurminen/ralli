using UnityEngine;

// First-person driver arms. Spawns the arms model, pins the shoulders at the driver's seat and
// bends each arm with a two-bone IK onto the steering wheel rim. Grip points ride on the wheel
// transform, so hands turn with it. The right hand moves to the handbrake while it is held.
// Bone names follow the WRAD ARMS rig (shoulder/bicep/forearm/wrist + finger_*.l/.r).
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CockpitAnimator))]
[RequireComponent(typeof(CarInputReader))]
public class DriverArms : MonoBehaviour
{
    private class Arm
    {
        public Transform shoulder;
        public Transform upper;
        public Transform lower;
        public Transform hand;
        public Quaternion upperRest;
        public Quaternion lowerRest;
        public Quaternion handRest;
        public Transform[] fingers;
        public Quaternion[] fingerRest;
        public bool[] isThumb;
        public Vector3 wheelGripLocal;
    }

    private static readonly string[] FingerNames = { "index", "middle", "ring", "pinky", "thumb" };

    [Header("Model")]
    [Tooltip("The arms model, e.g. ThirdParty/WRAD_ARMS/arms.fbx.")]
    [SerializeField] private GameObject armsPrefab;

    [Header("Body (car-local, meters)")]
    [SerializeField] private Vector3 leftShoulder = new Vector3(-0.51f, -0.19f, -0.23f);
    [SerializeField] private Vector3 rightShoulder = new Vector3(-0.13f, -0.19f, -0.23f);
    [Tooltip("Where the elbows point, relative to each shoulder. X is outward (mirrored for the left arm).")]
    [SerializeField] private Vector3 elbowPole = new Vector3(0.45f, -0.6f, 0.1f);

    [Header("Steering Wheel Grip")]
    [SerializeField] private float wheelRadius = 0.17f;
    [Tooltip("Hands this many degrees above the 9 and 3 o'clock positions.")]
    [SerializeField] private float gripAngle = 15f;
    [Tooltip("Moves grip points toward the driver so hands sit on the rim, not inside it (m).")]
    [SerializeField] private float gripBackOffset = 0.02f;

    [Header("Hands")]
    [Tooltip("Extra wrist rotation (local Euler) to line the palm up with the rim.")]
    [SerializeField] private Vector3 leftHandRotationOffset;
    [SerializeField] private Vector3 rightHandRotationOffset;
    [Tooltip("Finger curl around each finger bone's local X. Flip the sign if fingers bend backwards.")]
    [SerializeField] private float fingerCurlDegrees = 55f;
    [SerializeField] private float thumbCurlDegrees = 20f;

    [Header("Handbrake")]
    [SerializeField] private string handbrakePartName = "HandBrake";
    [Tooltip("Grip point relative to the handbrake part, in car axes (m).")]
    [SerializeField] private Vector3 handbrakeGripOffset = new Vector3(0f, 0.12f, 0f);
    [SerializeField] private float handSwitchTime = 0.2f;

    private CockpitAnimator cockpit;
    private CarInputReader input;
    private Transform handbrake;
    private Arm leftArm;
    private Arm rightArm;
    private float handbrakeBlend;

    private void Start()
    {
        cockpit = GetComponent<CockpitAnimator>();
        input = GetComponent<CarInputReader>();

        if (armsPrefab == null)
        {
            Debug.LogWarning("[DriverArms] No arms prefab assigned.");
            return;
        }

        GameObject arms = Instantiate(armsPrefab, transform);
        arms.name = "DriverArms";
        arms.transform.localPosition = Vector3.zero;
        arms.transform.localRotation = Quaternion.identity;

        // Bones get moved far from the model's bind pose, so never cull the mesh by stale bounds.
        foreach (SkinnedMeshRenderer skinned in arms.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skinned.updateWhenOffscreen = true;
        }

        leftArm = BindArm(arms.transform, "l");
        rightArm = BindArm(arms.transform, "r");
        handbrake = cockpit.FindModelPart(handbrakePartName);

        Transform wheel = cockpit.SteeringWheel;
        if (wheel != null)
        {
            if (leftArm != null)
            {
                leftArm.wheelGripLocal = wheel.InverseTransformPoint(GetRestGripPoint(wheel, -1f));
            }

            if (rightArm != null)
            {
                rightArm.wheelGripLocal = wheel.InverseTransformPoint(GetRestGripPoint(wheel, 1f));
            }
        }
    }

    private void LateUpdate()
    {
        Transform wheel = cockpit != null ? cockpit.SteeringWheel : null;
        if (wheel == null)
        {
            return;
        }

        float blendTarget = input.Handbrake && handbrake != null ? 1f : 0f;
        handbrakeBlend = Mathf.MoveTowards(handbrakeBlend, blendTarget, Time.deltaTime / Mathf.Max(0.01f, handSwitchTime));

        if (leftArm != null)
        {
            PoseArm(leftArm, leftShoulder, -1f, wheel.TransformPoint(leftArm.wheelGripLocal), leftHandRotationOffset);
        }

        if (rightArm != null)
        {
            Vector3 target = wheel.TransformPoint(rightArm.wheelGripLocal);
            if (handbrakeBlend > 0f && handbrake != null)
            {
                Vector3 brakeGrip = handbrake.position + transform.TransformDirection(handbrakeGripOffset);
                float t = handbrakeBlend * handbrakeBlend * (3f - 2f * handbrakeBlend);
                target = Vector3.Lerp(target, brakeGrip, t);
            }

            PoseArm(rightArm, rightShoulder, 1f, target, rightHandRotationOffset);
        }
    }

    // Rest grip point in world space: on the rim, gripAngle above 9 (side -1) or 3 (side +1) o'clock.
    private Vector3 GetRestGripPoint(Transform wheel, float side)
    {
        Vector3 shoulderMid = transform.TransformPoint((leftShoulder + rightShoulder) * 0.5f);
        Vector3 towardDriver = (shoulderMid - wheel.position).normalized;
        Vector3 right = Vector3.Cross(towardDriver, transform.up).normalized;

        // Positive angle about towardDriver is clockwise as the driver sees it.
        Vector3 spoke = Quaternion.AngleAxis(-side * gripAngle, towardDriver) * (right * side);
        return wheel.position + spoke * wheelRadius + towardDriver * gripBackOffset;
    }

    private void PoseArm(Arm arm, Vector3 shoulderLocal, float side, Vector3 target, Vector3 handRotationOffset)
    {
        arm.upper.localRotation = arm.upperRest;
        arm.lower.localRotation = arm.lowerRest;

        // Pin the shoulder joint to the driver's body.
        Vector3 shoulderWorld = transform.TransformPoint(shoulderLocal);
        arm.shoulder.position += shoulderWorld - arm.upper.position;

        Vector3 pole = shoulderWorld + transform.TransformDirection(new Vector3(side * elbowPole.x, elbowPole.y, elbowPole.z));
        SolveTwoBoneIK(arm.upper, arm.lower, arm.hand, target, pole);

        arm.hand.localRotation = arm.handRest * Quaternion.Euler(handRotationOffset);
        for (int i = 0; i < arm.fingers.Length; i++)
        {
            float curl = arm.isThumb[i] ? thumbCurlDegrees : fingerCurlDegrees;
            arm.fingers[i].localRotation = arm.fingerRest[i] * Quaternion.Euler(curl, 0f, 0f);
        }
    }

    // Classic analytic two-bone IK: place the elbow on the circle allowed by the bone lengths,
    // on the pole side, then aim upper bone at the elbow and lower bone at the target.
    private static void SolveTwoBoneIK(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
    {
        Vector3 a = upper.position;
        float upperLength = Vector3.Distance(a, lower.position);
        float lowerLength = Vector3.Distance(lower.position, end.position);
        Vector3 toTarget = target - a;
        float distance = Mathf.Clamp(toTarget.magnitude, 0.001f, upperLength + lowerLength - 0.001f);
        Vector3 direction = toTarget.normalized;

        float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
        float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        Vector3 bend = Vector3.ProjectOnPlane(pole - a, direction);
        if (bend.sqrMagnitude < 0.000001f)
        {
            bend = Vector3.ProjectOnPlane(Vector3.down, direction);
        }

        Vector3 elbow = a + direction * along + bend.normalized * height;
        upper.rotation = Quaternion.FromToRotation(lower.position - a, elbow - a) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position, target - lower.position) * lower.rotation;
    }

    private static Arm BindArm(Transform root, string suffix)
    {
        Transform shoulder = FindDeep(root, $"shoulder.{suffix}");
        Transform upper = FindDeep(root, $"bicep.{suffix}");
        Transform lower = FindDeep(root, $"forearm.{suffix}");
        Transform hand = FindDeep(root, $"wrist.{suffix}");
        if (shoulder == null || upper == null || lower == null || hand == null)
        {
            Debug.LogWarning($"[DriverArms] Missing arm bones for side '{suffix}'.");
            return null;
        }

        var fingers = new System.Collections.Generic.List<Transform>();
        var thumbs = new System.Collections.Generic.List<bool>();
        foreach (string finger in FingerNames)
        {
            for (int joint = 1; joint <= 3; joint++)
            {
                Transform bone = FindDeep(root, $"finger_{finger}{joint}.{suffix}");
                if (bone != null)
                {
                    fingers.Add(bone);
                    thumbs.Add(finger == "thumb");
                }
            }
        }

        var arm = new Arm
        {
            shoulder = shoulder,
            upper = upper,
            lower = lower,
            hand = hand,
            upperRest = upper.localRotation,
            lowerRest = lower.localRotation,
            handRest = hand.localRotation,
            fingers = fingers.ToArray(),
            isThumb = thumbs.ToArray(),
            fingerRest = new Quaternion[fingers.Count]
        };

        for (int i = 0; i < arm.fingers.Length; i++)
        {
            arm.fingerRest[i] = arm.fingers[i].localRotation;
        }

        return arm;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
