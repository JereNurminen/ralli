using UnityEngine;

// Drives the cockpit model's steering wheel and gauge needles from the car state.
// Parts are found by name under the model's LOD0, so any model using the same part names
// works. Each part rotates about its own axis on top of its modelled rest pose.
// Signs assume the model is mirrored to left-hand drive; flip them if a part turns backwards.
[RequireComponent(typeof(CarController))]
public class CockpitAnimator : MonoBehaviour
{
    [System.Serializable]
    private class Needle
    {
        public string partName;
        [Tooltip("Degrees the needle travels from empty to full scale. Negative flips direction.")]
        public float sweepDegrees = 240f;
        [HideInInspector] public Transform part;
        [HideInInspector] public Quaternion restRotation;
        [HideInInspector] public float value01;
        [HideInInspector] public float velocity;
    }

    private const float MpsToKph = 3.6f;

    [Header("Model")]
    [Tooltip("Car model root. Empty = search this object's children.")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private string lodName = "LOD0";
    [Tooltip("Local axis the steering wheel and needles spin around.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;

    [Header("Steering Wheel")]
    [SerializeField] private string steeringWheelName = "SteeringWheel";
    [Tooltip("Steering wheel degrees per front-wheel degree. Negative flips direction.")]
    [SerializeField] private float steeringRatio = 4f;
    [SerializeField] private float maxSteeringWheelDegrees = 180f;
    [Tooltip("How long the wheel takes to catch up with the steering (s). Visual only.")]
    [SerializeField] private float steeringWheelSmoothTime = 0.08f;

    [Header("Speedometer")]
    [SerializeField] private Needle speedometer = new Needle { partName = "Gauge_Speed_Needle", sweepDegrees = 240f };
    [SerializeField] private float speedometerMaxKph = 240f;

    [Header("Tachometer")]
    [SerializeField] private Needle tachometer = new Needle { partName = "Gauge_Tacho_Needle", sweepDegrees = 240f };

    [Header("Heat Gauge (fuel needle)")]
    [SerializeField] private Needle heatGauge = new Needle { partName = "Gauge_Fuel_Needle", sweepDegrees = -90f };

    [Header("Smoothing")]
    [SerializeField] private float needleSmoothTime = 0.08f;
    [Tooltip("Fastest a needle can swing (degrees per second). Stops the rev needle snapping on shifts.")]
    [SerializeField] private float needleMaxDegreesPerSecond = 180f;

    private CarController car;
    private Transform steeringWheel;
    private Quaternion steeringWheelRest;
    private float wheelDegrees;
    private float wheelVelocity;
    private Transform lodRoot;

    public Transform SteeringWheel => steeringWheel;

    // Finds a named part of the cockpit model (under LOD0). Null if absent or not yet bound.
    public Transform FindModelPart(string partName)
    {
        return lodRoot != null ? FindDeep(lodRoot, partName) : null;
    }

    private void Start()
    {
        car = GetComponent<CarController>();

        Transform searchRoot = modelRoot != null ? modelRoot : transform;
        Transform lod = FindDeep(searchRoot, lodName);
        lodRoot = lod;
        if (lod == null)
        {
            Debug.LogWarning($"[CockpitAnimator] No '{lodName}' found under {searchRoot.name}; cockpit stays static.");
            return;
        }

        steeringWheel = FindDeep(lod, steeringWheelName);
        if (steeringWheel != null)
        {
            steeringWheelRest = steeringWheel.localRotation;
        }

        BindNeedle(speedometer, lod);
        BindNeedle(tachometer, lod);
        BindNeedle(heatGauge, lod);
    }

    // Update (not LateUpdate) so the driver arms rig, which evaluates before LateUpdate,
    // sees this frame's steering wheel angle.
    private void Update()
    {
        if (car == null)
        {
            return;
        }

        if (steeringWheel != null)
        {
            float targetDegrees = Mathf.Clamp(car.SteerAngleDegrees * steeringRatio, -maxSteeringWheelDegrees, maxSteeringWheelDegrees);
            wheelDegrees = Mathf.SmoothDamp(wheelDegrees, targetDegrees, ref wheelVelocity, steeringWheelSmoothTime);
            steeringWheel.localRotation = steeringWheelRest * Quaternion.AngleAxis(wheelDegrees, rotationAxis);
        }

        float speedKph = car.SpeedMps * MpsToKph;
        UpdateNeedle(speedometer, speedKph / Mathf.Max(1f, speedometerMaxKph));
        UpdateNeedle(tachometer, car.EngineRpm01);
        UpdateNeedle(heatGauge, car.Heat01);
    }

    private void BindNeedle(Needle needle, Transform lod)
    {
        needle.part = FindDeep(lod, needle.partName);
        if (needle.part != null)
        {
            needle.restRotation = needle.part.localRotation;
        }
    }

    private void UpdateNeedle(Needle needle, float target01)
    {
        if (needle.part == null)
        {
            return;
        }

        float maxSpeed01 = needleMaxDegreesPerSecond / Mathf.Max(1f, Mathf.Abs(needle.sweepDegrees));
        needle.value01 = Mathf.SmoothDamp(needle.value01, Mathf.Clamp01(target01), ref needle.velocity, needleSmoothTime, maxSpeed01);
        needle.part.localRotation = needle.restRotation * Quaternion.AngleAxis(needle.value01 * needle.sweepDegrees, rotationAxis);
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
