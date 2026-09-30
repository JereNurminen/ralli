using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// Builds the driver arms rig in the open scene: the arms model under the car, Animation Rigging
// two-bone IK and forearm twist correction per arm, and draggable palm anchors on the steering
// wheel and handbrake. Each anchor's blue axis is where the fingers point, green is the thumb side;
// its WristTarget child keeps the palm (not the wrist) on the anchor. Re-running rebuilds from
// scratch, so anchor tweaks are lost; save the scene after running.
public static class DriverArmsRigBuilder
{
    private const string ArmsAssetPath = "Assets/ThirdParty/WRAD_ARMS/arms.fbx";
    private const float ArmsScale = 0.09f;
    private const float WheelRadius = 0.17f;
    private const float GripAngle = 15f;
    private const float GripOutset = 0.02f;
    private const float FingerInwardTilt = 0.35f;
    private const float TwistNodeWeight = 0.33f;
    private static readonly Vector3 ShoulderMidLocal = new Vector3(-0.32f, -0.19f, -0.23f);
    private static readonly Vector3 ElbowHintOffset = new Vector3(0.45f, -0.6f, 0.1f);
    private static readonly Vector3 HandbrakeGripOffset = new Vector3(0f, 0.12f, 0f);

    [MenuItem("Ralli/Build Driver Arms Rig")]
    private static void Build()
    {
        CarController car = Object.FindFirstObjectByType<CarController>();
        GameObject armsAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsAssetPath);
        Transform wheel = car != null ? FindModelPart(car.transform, "SteeringWheel") : null;
        if (car == null || armsAsset == null || wheel == null)
        {
            EditorUtility.DisplayDialog("Driver Arms",
                "Needs a CarController in the open scene, the arms model at " + ArmsAssetPath +
                " and a car model with LOD0/SteeringWheel.", "OK");
            return;
        }

        Transform carT = car.transform;
        Transform handbrakePart = FindModelPart(carT, "HandBrake");
        RemoveChild(carT, "DriverArms");
        RemoveChild(carT, "DriverArmsTargets");

        GameObject arms = PlaceArms(armsAsset, carT);
        GameObject rig = CreateRig(arms);

        var targets = CreateChild("DriverArmsTargets", carT);
        Undo.RegisterCreatedObjectUndo(targets, "Build Driver Arms Rig");

        GameObject wheelGrips = CreateChild("WheelGrips", targets.transform);
        wheelGrips.transform.SetPositionAndRotation(wheel.position, carT.rotation);
        var follower = wheelGrips.AddComponent<SteeringWheelFollower>();
        SetReference(follower, "wheel", wheel);

        Vector3 towardDriver = (carT.TransformPoint(ShoulderMidLocal) - wheel.position).normalized;
        Transform leftWrist = CreateWheelGrip(arms.transform, "l", -1f, wheel, towardDriver, carT, wheelGrips.transform);
        Transform rightWrist = CreateWheelGrip(arms.transform, "r", 1f, wheel, towardDriver, carT, wheelGrips.transform);

        Transform handbrakeWrist = null;
        if (handbrakePart != null)
        {
            // Palm down on the lever, fingers forward.
            Quaternion handbrakeRotation = GetHandRotation(arms.transform, "r", carT.forward, -carT.right);
            Vector3 palm = handbrakePart.position + carT.TransformDirection(HandbrakeGripOffset);
            handbrakeWrist = CreateGrip("HandbrakeGrip", targets.transform, palm, handbrakeRotation, GetPalmOffset(arms.transform, "r"));
        }

        GameObject rightHandTarget = CreateChild("RightHandTarget", targets.transform);
        rightHandTarget.transform.SetPositionAndRotation(rightWrist.position, rightWrist.rotation);

        Transform leftHint = CreateElbowHint(arms.transform, "l", -1f, carT, targets.transform);
        Transform rightHint = CreateElbowHint(arms.transform, "r", 1f, carT, targets.transform);

        AddArmConstraints(rig.transform, "Left", arms.transform, "l", leftWrist, leftHint);
        AddArmConstraints(rig.transform, "Right", arms.transform, "r", rightHandTarget.transform, rightHint);

        DriverArms driverArms = car.GetComponent<DriverArms>();
        if (driverArms == null)
        {
            driverArms = Undo.AddComponent<DriverArms>(car.gameObject);
        }

        SetReference(driverArms, "armsRoot", arms.transform);
        SetReference(driverArms, "rightHandTarget", rightHandTarget.transform);
        SetReference(driverArms, "rightWheelTarget", rightWrist);
        SetReference(driverArms, "handbrakeTarget", handbrakeWrist);

        EditorSceneManager.MarkSceneDirty(car.gameObject.scene);
        Selection.activeGameObject = wheelGrips;
        Debug.Log("[DriverArms] Rig built. Move the Grip_L / Grip_R / HandbrakeGrip anchors, then save the scene.");
    }

    private static GameObject PlaceArms(GameObject armsAsset, Transform carT)
    {
        var arms = (GameObject)PrefabUtility.InstantiatePrefab(armsAsset, carT);
        Undo.RegisterCreatedObjectUndo(arms, "Build Driver Arms Rig");
        arms.name = "DriverArms";
        arms.transform.localPosition = Vector3.zero;
        arms.transform.localRotation = Quaternion.identity;
        arms.transform.localScale = Vector3.one * ArmsScale;

        // The left arm must end up on the car's left.
        Transform bicepL = Find(arms.transform, "bicep.l");
        Transform bicepR = Find(arms.transform, "bicep.r");
        if (carT.InverseTransformPoint(bicepL.position).x > carT.InverseTransformPoint(bicepR.position).x)
        {
            arms.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        Vector3 shoulderMid = (bicepL.position + bicepR.position) * 0.5f;
        arms.transform.position += carT.TransformPoint(ShoulderMidLocal) - shoulderMid;

        foreach (SkinnedMeshRenderer skinned in arms.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skinned.updateWhenOffscreen = true;
        }

        return arms;
    }

    private static GameObject CreateRig(GameObject arms)
    {
        Animator animator = arms.GetComponent<Animator>();
        if (animator == null)
        {
            animator = arms.AddComponent<Animator>();
        }

        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        RigBuilder rigBuilder = arms.AddComponent<RigBuilder>();
        GameObject rig = CreateChild("Rig", arms.transform);
        rigBuilder.layers.Add(new RigLayer(rig.AddComponent<Rig>()));
        return rig;
    }

    // 9-and-3 grip, GripAngle above horizontal: palm on the outside of the rim facing the wheel
    // center, fingers forward (tilted inward) around the rim, thumb up along the rim.
    private static Transform CreateWheelGrip(Transform arms, string side, float sideSign, Transform wheel, Vector3 towardDriver, Transform carT, Transform parent)
    {
        Vector3 right = Vector3.Cross(towardDriver, carT.up).normalized;
        Vector3 up = Vector3.Cross(right, towardDriver).normalized;

        // Positive angle about towardDriver is clockwise as the driver sees it.
        Quaternion raise = Quaternion.AngleAxis(-sideSign * GripAngle, towardDriver);
        Vector3 spoke = raise * (right * sideSign);
        Vector3 palm = wheel.position + spoke * (WheelRadius + GripOutset);
        Vector3 fingerAim = (-towardDriver - spoke * FingerInwardTilt).normalized;
        Quaternion rotation = GetHandRotation(arms, side, fingerAim, raise * up);

        string name = sideSign < 0f ? "Grip_L" : "Grip_R";
        return CreateGrip(name, parent, palm, rotation, GetPalmOffset(arms, side));
    }

    // Anchor at the palm, oriented like the wrist; its WristTarget child is where the wrist must go.
    private static Transform CreateGrip(string name, Transform parent, Vector3 palmPosition, Quaternion handRotation, Vector3 palmOffset)
    {
        GameObject grip = CreateChild(name, parent);
        grip.transform.SetPositionAndRotation(palmPosition, handRotation);
        GameObject wristTarget = CreateChild("WristTarget", grip.transform);
        wristTarget.transform.localPosition = -palmOffset;
        wristTarget.transform.localRotation = Quaternion.identity;
        return wristTarget.transform;
    }

    private static Transform CreateElbowHint(Transform arms, string side, float sideSign, Transform carT, Transform parent)
    {
        Transform bicep = Find(arms, $"bicep.{side}");
        GameObject hint = CreateChild(sideSign < 0f ? "ElbowHint_L" : "ElbowHint_R", parent);
        Vector3 offset = new Vector3(sideSign * ElbowHintOffset.x, ElbowHintOffset.y, ElbowHintOffset.z);
        hint.transform.position = bicep.position + carT.TransformDirection(offset);
        return hint.transform;
    }

    private static void AddArmConstraints(Transform rig, string prefix, Transform arms, string side, Transform target, Transform hint)
    {
        Transform bicep = Find(arms, $"bicep.{side}");
        Transform forearm = Find(arms, $"forearm.{side}");
        Transform wrist = Find(arms, $"wrist.{side}");

        TwoBoneIKConstraint ik = CreateChild($"{prefix}ArmIK", rig).AddComponent<TwoBoneIKConstraint>();
        ik.data.root = bicep;
        ik.data.mid = forearm;
        ik.data.tip = wrist;
        ik.data.target = target;
        ik.data.hint = hint;
        ik.data.targetPositionWeight = 1f;
        ik.data.targetRotationWeight = 1f;
        ik.data.hintWeight = 1f;

        // Spread the wrist's roll over the forearm twist bones (Twist1 is a child of Twist0, so it gets both).
        TwistCorrection twist = CreateChild($"{prefix}ForearmTwist", rig).AddComponent<TwistCorrection>();
        twist.data.sourceObject = wrist;
        twist.data.twistAxis = GetBoneAxis(wrist, wrist.position - forearm.position);
        var nodes = new WeightedTransformArray(0);
        foreach (string twistName in new[] { $"forearm.Twist0.{side}", $"forearm.Twist1.{side}" })
        {
            Transform node = Find(arms, twistName);
            if (node != null)
            {
                nodes.Add(new WeightedTransform(node, TwistNodeWeight));
            }
        }

        twist.data.twistNodes = nodes;
    }

    // World rotation that points the hand's fingers along fingerAim with its thumb side toward thumbSide.
    // The hand's own axes are measured from the rig: wrist→middle knuckle, pinky→index knuckle.
    private static Quaternion GetHandRotation(Transform arms, string side, Vector3 fingerAim, Vector3 thumbSide)
    {
        Transform wrist = Find(arms, $"wrist.{side}");
        Transform middle = Find(arms, $"finger_middle1.{side}");
        Transform index = Find(arms, $"finger_index1.{side}");
        Transform pinky = Find(arms, $"finger_pinky1.{side}");
        Vector3 fingerLocal = wrist.InverseTransformDirection(middle.position - wrist.position);
        Vector3 thumbLocal = wrist.InverseTransformDirection(index.position - pinky.position);
        return Quaternion.LookRotation(fingerAim, thumbSide) * Quaternion.Inverse(Quaternion.LookRotation(fingerLocal, thumbLocal));
    }

    // Palm center relative to the wrist, in the wrist's frame (world-scale meters).
    private static Vector3 GetPalmOffset(Transform arms, string side)
    {
        Transform wrist = Find(arms, $"wrist.{side}");
        Transform index = Find(arms, $"finger_index1.{side}");
        Transform pinky = Find(arms, $"finger_pinky1.{side}");
        Vector3 knuckles = (index.position + pinky.position) * 0.5f;
        Vector3 palm = Vector3.Lerp(wrist.position, knuckles, 0.6f);
        return Quaternion.Inverse(wrist.rotation) * (palm - wrist.position);
    }

    private static TwistCorrectionData.Axis GetBoneAxis(Transform bone, Vector3 worldDirection)
    {
        Vector3 local = bone.InverseTransformDirection(worldDirection);
        Vector3 abs = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
        if (abs.x >= abs.y && abs.x >= abs.z)
        {
            return TwistCorrectionData.Axis.X;
        }

        return abs.y >= abs.z ? TwistCorrectionData.Axis.Y : TwistCorrectionData.Axis.Z;
    }

    private static Transform FindModelPart(Transform carT, string partName)
    {
        Transform lod = Find(carT, "LOD0");
        return lod != null ? Find(lod, partName) : null;
    }

    private static void RemoveChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void SetReference(Object target, string property, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static Transform Find(Transform root, string name)
    {
        return DriverArms.FindDeep(root, name);
    }
}
