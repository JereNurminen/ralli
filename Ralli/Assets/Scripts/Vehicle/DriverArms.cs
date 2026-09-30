using UnityEngine;

// Runtime side of the driver arms rig. Arm IK and forearm twist are Animation Rigging constraints
// (built by Ralli > Build Driver Arms Rig). This moves the right hand's IK target between the
// wheel grip and the handbrake, and curls the fingers into a fixed grip after the rig has run.
[DefaultExecutionOrder(60)]
[RequireComponent(typeof(CarInputReader))]
public class DriverArms : MonoBehaviour
{
    private static readonly string[] FingerNames = { "index", "middle", "ring", "pinky", "thumb" };

    [Header("Rig")]
    [SerializeField] private Transform armsRoot;
    [Tooltip("IK target the right arm constraint follows. Moved here every frame.")]
    [SerializeField] private Transform rightHandTarget;
    [SerializeField] private Transform rightWheelTarget;
    [SerializeField] private Transform handbrakeTarget;

    [Header("Handbrake")]
    [SerializeField] private float handSwitchTime = 0.2f;

    [Header("Fingers")]
    [Tooltip("Finger curl around each finger bone's local X. Flip the sign if fingers bend backwards.")]
    [SerializeField] private float fingerCurlDegrees = 55f;
    [SerializeField] private float thumbCurlDegrees = 20f;

    private CarInputReader input;
    private float handbrakeBlend;
    private Transform[] fingers;
    private Quaternion[] fingerRest;
    private bool[] isThumb;

    private void Start()
    {
        input = GetComponent<CarInputReader>();
        BindFingers();
    }

    private void Update()
    {
        if (rightHandTarget == null || rightWheelTarget == null)
        {
            return;
        }

        bool toHandbrake = input.Handbrake && handbrakeTarget != null;
        handbrakeBlend = Mathf.MoveTowards(handbrakeBlend, toHandbrake ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, handSwitchTime));

        if (handbrakeBlend <= 0f || handbrakeTarget == null)
        {
            rightHandTarget.SetPositionAndRotation(rightWheelTarget.position, rightWheelTarget.rotation);
            return;
        }

        float t = handbrakeBlend * handbrakeBlend * (3f - 2f * handbrakeBlend);
        rightHandTarget.SetPositionAndRotation(
            Vector3.Lerp(rightWheelTarget.position, handbrakeTarget.position, t),
            Quaternion.Slerp(rightWheelTarget.rotation, handbrakeTarget.rotation, t)
        );
    }

    private void LateUpdate()
    {
        if (fingers == null)
        {
            return;
        }

        for (int i = 0; i < fingers.Length; i++)
        {
            float curl = isThumb[i] ? thumbCurlDegrees : fingerCurlDegrees;
            fingers[i].localRotation = fingerRest[i] * Quaternion.Euler(curl, 0f, 0f);
        }
    }

    private void BindFingers()
    {
        if (armsRoot == null)
        {
            return;
        }

        var found = new System.Collections.Generic.List<Transform>();
        var thumbs = new System.Collections.Generic.List<bool>();
        foreach (string side in new[] { "l", "r" })
        {
            foreach (string finger in FingerNames)
            {
                for (int joint = 1; joint <= 3; joint++)
                {
                    Transform bone = FindDeep(armsRoot, $"finger_{finger}{joint}.{side}");
                    if (bone != null)
                    {
                        found.Add(bone);
                        thumbs.Add(finger == "thumb");
                    }
                }
            }
        }

        fingers = found.ToArray();
        isThumb = thumbs.ToArray();
        fingerRest = new Quaternion[fingers.Length];
        for (int i = 0; i < fingers.Length; i++)
        {
            fingerRest[i] = fingers[i].localRotation;
        }
    }

    public static Transform FindDeep(Transform root, string name)
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
