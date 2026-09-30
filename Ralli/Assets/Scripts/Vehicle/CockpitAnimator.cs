using UnityEngine;

// Drives the cockpit model's steering wheel and gauge needles from the car state.
// Parts are found by name under the model's LOD0, so any model using the same part names
// works. Each part rotates about its own axis on top of its modelled rest pose.
// Signs assume the model is mirrored to left-hand drive; flip them if a part turns backwards.
[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(CarInputReader))]
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

    [Header("Speedometer")]
    [SerializeField] private Needle speedometer = new Needle { partName = "Gauge_Speed_Needle", sweepDegrees = 240f };
    [SerializeField] private float speedometerMaxKph = 240f;

    [Header("Tachometer (fake gears from speed)")]
    [SerializeField] private Needle tachometer = new Needle { partName = "Gauge_Tacho_Needle", sweepDegrees = 240f };
    [Tooltip("Speed range covered by each fake gear (km/h).")]
    [SerializeField] private float gearSpanKph = 45f;
    [SerializeField] private int gearCount = 5;
    [Range(0f, 1f)] [SerializeField] private float idleRpm01 = 0.15f;

    [Header("Heat Gauge (fuel needle)")]
    [SerializeField] private Needle heatGauge = new Needle { partName = "Gauge_Fuel_Needle", sweepDegrees = -90f };

    [Header("Smoothing")]
    [SerializeField] private float needleSmoothTime = 0.08f;
    [Tooltip("Fastest a needle can swing (degrees per second). Stops the rev needle snapping on shifts.")]
    [SerializeField] private float needleMaxDegreesPerSecond = 180f;

    private CarController car;
    private CarInputReader input;
    private Transform steeringWheel;
    private Quaternion steeringWheelRest;

    private void Start()
    {
        car = GetComponent<CarController>();
        input = GetComponent<CarInputReader>();

        Transform searchRoot = modelRoot != null ? modelRoot : transform;
        Transform lod = FindDeep(searchRoot, lodName);
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

    private void LateUpdate()
    {
        if (car == null)
        {
            return;
        }

        if (steeringWheel != null)
        {
            float wheelDegrees = Mathf.Clamp(car.SteerAngleDegrees * steeringRatio, -maxSteeringWheelDegrees, maxSteeringWheelDegrees);
            steeringWheel.localRotation = steeringWheelRest * Quaternion.AngleAxis(wheelDegrees, rotationAxis);
        }

        float speedKph = car.SpeedMps * MpsToKph;
        UpdateNeedle(speedometer, speedKph / Mathf.Max(1f, speedometerMaxKph));
        UpdateNeedle(tachometer, GetFakeRpm01(speedKph));
        UpdateNeedle(heatGauge, car.Heat01);
    }

    // Revs climb through each gear's speed span and drop at the "shift"; throttle adds a bit on top.
    private float GetFakeRpm01(float speedKph)
    {
        float span = Mathf.Max(1f, gearSpanKph);
        int gear = Mathf.Min(Mathf.FloorToInt(speedKph / span), Mathf.Max(1, gearCount) - 1);
        float inGear = Mathf.Clamp01((speedKph - gear * span) / span);
        float rpm01 = Mathf.Lerp(idleRpm01 + 0.2f, 0.9f, inGear);
        if (speedKph < 3f)
        {
            rpm01 = idleRpm01;
        }

        return Mathf.Clamp01(rpm01 + input.Throttle * 0.08f);
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
