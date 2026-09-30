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
        // Forearm twist bones (if the rig has them), ordered elbow → wrist.
        public Transform[] twistBones;
        public Quaternion[] twistRest;
        public Transform[] fingers;
        public Quaternion[] fingerRest;
        public bool[] isThumb;
        // Hand's own frame, measured from the rig: wrist→middle finger, and pinky→index (thumb side).
        public Vector3 fingerDirLocal;
        public Vector3 thumbSideLocal;
        public float handLength;
        // Grip on the steering wheel, stored in the wheel's space so it turns with the wheel.
        public Vector3 rimLocal;
        public Vector3 inwardLocal;
        public Vector3 fingerAimLocal;
        public Vector3 thumbUpLocal;
    }

    private static readonly string[] FingerNames = { "index", "middle", "ring", "pinky", "thumb" };

    [Header("Model")]
    [Tooltip("The arms model, e.g. ThirdParty/WRAD_ARMS/arms.fbx.")]
    [SerializeField] private GameObject armsPrefab;
    [Tooltip("Uniform scale applied to the spawned arms. WRAD ARMS imports far too large; ~0.09 fits.")]
    [SerializeField] private float armsScale = 0.09f;

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
    [Tooltip("Wrist sits this far outside the rim, so the palm wraps it rather than the wrist (m).")]
    [SerializeField] private float gripOutset = 0.03f;
    [Tooltip("How much the fingers angle in toward the wheel center (0 = straight forward).")]
    [SerializeField] private float fingerInwardTilt = 0.35f;
    [Tooltip("Fraction of hand length the wrist sits behind the rim, so the palm lands on it.")]
    [Range(0f, 1f)] [SerializeField] private float palmFraction = 0.5f;

    [Header("Hands")]
    [Tooltip("Extra wrist rotation (local Euler) on top of the computed grip. Usually leave at zero.")]
    [SerializeField] private Vector3 leftHandRotationOffset;
    [SerializeField] private Vector3 rightHandRotationOffset;
    [Tooltip("Finger curl around each finger bone's local X. Flip the sign if fingers bend backwards.")]
    [SerializeField] private float fingerCurlDegrees = 55f;
    [SerializeField] private float thumbCurlDegrees = 20f;
    [Tooltip("Share of the forearm roll taken at the elbow end. Twist bones and the wrist take the rest progressively.")]
    [Range(0f, 1f)] [SerializeField] private float forearmTwistShare = 0.3f;

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
        arms.transform.localScale = Vector3.one * armsScale;

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
                SetupWheelGrip(leftArm, wheel, -1f);
            }

            if (rightArm != null)
            {
                SetupWheelGrip(rightArm, wheel, 1f);
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
            GetWheelGrip(leftArm, wheel, out Vector3 leftTarget, out Quaternion leftRotation);
            PoseArm(leftArm, leftShoulder, -1f, leftTarget, leftRotation, leftHandRotationOffset);
        }

        if (rightArm != null)
        {
            GetWheelGrip(rightArm, wheel, out Vector3 target, out Quaternion rotation);
            if (handbrakeBlend > 0f && handbrake != null)
            {
                // Palm down on the lever, fingers forward.
                Quaternion brakeRotation = GetHandRotation(rightArm, transform.forward, -transform.right);
                Vector3 brakeGrip = handbrake.position + transform.TransformDirection(handbrakeGripOffset);
                Vector3 brakeTarget = brakeGrip - transform.forward * (rightArm.handLength * palmFraction);
                float t = handbrakeBlend * handbrakeBlend * (3f - 2f * handbrakeBlend);
                target = Vector3.Lerp(target, brakeTarget, t);
                rotation = Quaternion.Slerp(rotation, brakeRotation, t);
            }

            PoseArm(rightArm, rightShoulder, 1f, target, rotation, rightHandRotationOffset);
        }
    }

    // 9-and-3 grip, gripAngle above horizontal: palm faces the wheel center, fingers point
    // forward (tilted inward) around the rim, thumb up along the rim.
    private void SetupWheelGrip(Arm arm, Transform wheel, float side)
    {
        Vector3 shoulderMid = transform.TransformPoint((leftShoulder + rightShoulder) * 0.5f);
        Vector3 towardDriver = (shoulderMid - wheel.position).normalized;
        Vector3 right = Vector3.Cross(towardDriver, transform.up).normalized;
        Vector3 up = Vector3.Cross(right, towardDriver).normalized;

        // Positive angle about towardDriver is clockwise as the driver sees it.
        Quaternion raise = Quaternion.AngleAxis(-side * gripAngle, towardDriver);
        Vector3 spoke = raise * (right * side);
        Vector3 rim = wheel.position + spoke * wheelRadius + towardDriver * gripBackOffset;
        Vector3 inward = -spoke;
        Vector3 fingerAim = (-towardDriver + inward * fingerInwardTilt).normalized;

        arm.rimLocal = wheel.InverseTransformPoint(rim);
        arm.inwardLocal = wheel.InverseTransformVector(inward);
        arm.fingerAimLocal = wheel.InverseTransformVector(fingerAim);
        arm.thumbUpLocal = wheel.InverseTransformVector(raise * up);
    }

    private void GetWheelGrip(Arm arm, Transform wheel, out Vector3 wristTarget, out Quaternion handRotation)
    {
        Vector3 rim = wheel.TransformPoint(arm.rimLocal);
        Vector3 inward = wheel.TransformVector(arm.inwardLocal).normalized;
        Vector3 aim = wheel.TransformVector(arm.fingerAimLocal).normalized;
        Vector3 thumbUp = wheel.TransformVector(arm.thumbUpLocal).normalized;

        wristTarget = rim - inward * gripOutset - aim * (arm.handLength * palmFraction);
        handRotation = GetHandRotation(arm, aim, thumbUp);
    }

    // World rotation that points the hand's fingers along fingerAim with its thumb side toward thumbSide.
    private static Quaternion GetHandRotation(Arm arm, Vector3 fingerAim, Vector3 thumbSide)
    {
        return Quaternion.LookRotation(fingerAim, thumbSide)
               * Quaternion.Inverse(Quaternion.LookRotation(arm.fingerDirLocal, arm.thumbSideLocal));
    }

    private void PoseArm(Arm arm, Vector3 shoulderLocal, float side, Vector3 target, Quaternion handRotation, Vector3 handRotationOffset)
    {
        arm.upper.localRotation = arm.upperRest;
        arm.lower.localRotation = arm.lowerRest;

        // Pin the shoulder joint to the driver's body.
        Vector3 shoulderWorld = transform.TransformPoint(shoulderLocal);
        arm.shoulder.position += shoulderWorld - arm.upper.position;

        Vector3 pole = shoulderWorld + transform.TransformDirection(new Vector3(side * elbowPole.x, elbowPole.y, elbowPole.z));
        SolveTwoBoneIK(arm.upper, arm.lower, arm.hand, target, pole);

        ApplyForearmTwist(arm, handRotation * Quaternion.Euler(handRotationOffset));
        for (int i = 0; i < arm.fingers.Length; i++)
        {
            float curl = arm.isThumb[i] ? thumbCurlDegrees : fingerCurlDegrees;
            arm.fingers[i].localRotation = arm.fingerRest[i] * Quaternion.Euler(curl, 0f, 0f);
        }
    }

    // Rolls the forearm about its own length so the hand's twist is spread from elbow to wrist
    // instead of all happening at the wrist. Rolling about that axis never moves the wrist.
    private void ApplyForearmTwist(Arm arm, Quaternion desiredHand)
    {
        arm.hand.localRotation = arm.handRest;
        for (int i = 0; i < arm.twistBones.Length; i++)
        {
            arm.twistBones[i].localRotation = arm.twistRest[i];
        }

        Vector3 axis = (arm.hand.position - arm.lower.position).normalized;
        float roll = GetTwistDegrees(desiredHand * Quaternion.Inverse(arm.hand.rotation), axis);

        float forearmRoll = roll * forearmTwistShare;
        arm.lower.rotation = Quaternion.AngleAxis(forearmRoll, axis) * arm.lower.rotation;

        // Twist bones ramp from the forearm share up toward the full roll.
        int count = arm.twistBones.Length;
        for (int i = 0; i < count; i++)
        {
            float share = Mathf.Lerp(forearmTwistShare, 1f, (i + 1f) / (count + 1f));
            float inherited = GetInheritedTwistShare(arm, arm.twistBones[i].parent, count);
            arm.twistBones[i].rotation = Quaternion.AngleAxis(roll * (share - inherited), axis) * arm.twistBones[i].rotation;
        }

        arm.hand.rotation = desiredHand;
    }

    // Roll share already applied to a twist bone through its parent chain.
    private float GetInheritedTwistShare(Arm arm, Transform parent, int count)
    {
        for (int i = count - 1; i >= 0; i--)
        {
            if (parent == arm.twistBones[i])
            {
                return Mathf.Lerp(forearmTwistShare, 1f, (i + 1f) / (count + 1f));
            }
        }

        return forearmTwistShare;
    }

    // Angle (degrees, -180..180) of the part of a rotation that spins about the given axis.
    private static float GetTwistDegrees(Quaternion rotation, Vector3 axis)
    {
        float along = rotation.x * axis.x + rotation.y * axis.y + rotation.z * axis.z;
        float angle = 2f * Mathf.Atan2(along, rotation.w) * Mathf.Rad2Deg;
        return Mathf.DeltaAngle(0f, angle);
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

        var twists = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < 4; i++)
        {
            Transform twist = FindDeep(root, $"forearm.Twist{i}.{suffix}");
            if (twist != null)
            {
                twists.Add(twist);
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
            twistBones = twists.ToArray(),
            twistRest = new Quaternion[twists.Count],
            fingers = fingers.ToArray(),
            isThumb = thumbs.ToArray(),
            fingerRest = new Quaternion[fingers.Count]
        };

        for (int i = 0; i < arm.fingers.Length; i++)
        {
            arm.fingerRest[i] = arm.fingers[i].localRotation;
        }

        for (int i = 0; i < arm.twistBones.Length; i++)
        {
            arm.twistRest[i] = arm.twistBones[i].localRotation;
        }

        Transform middle = FindDeep(root, $"finger_middle1.{suffix}");
        Transform index = FindDeep(root, $"finger_index1.{suffix}");
        Transform pinky = FindDeep(root, $"finger_pinky1.{suffix}");
        if (middle != null && index != null && pinky != null)
        {
            arm.fingerDirLocal = hand.InverseTransformDirection(middle.position - hand.position).normalized;
            arm.thumbSideLocal = hand.InverseTransformDirection(index.position - pinky.position).normalized;
            arm.handLength = Vector3.Distance(hand.position, middle.position) * 2f;
        }
        else
        {
            arm.fingerDirLocal = Vector3.forward;
            arm.thumbSideLocal = Vector3.up;
            arm.handLength = 0.18f;
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
