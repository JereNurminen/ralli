using UnityEngine;

[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(Rigidbody))]
public class CarWheelVisuals : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private bool matchPhysicsWheelRadius = true;
    [SerializeField] private float visualWheelDiameter = 0.9f;
    [SerializeField] private float visualRadiusMultiplier = 1f;
    [SerializeField] private float wheelWidth = 0.28f;
    [SerializeField] private Material wheelMaterial;
    [SerializeField] private bool createVisualsInEditMode = true;

    private static readonly string[] WheelNames =
    {
        "WheelVisual_FL",
        "WheelVisual_FR",
        "WheelVisual_RL",
        "WheelVisual_RR"
    };

    private CarController carController;
    private Rigidbody rb;
    private Transform visualRoot;
    private readonly Transform[] wheelVisuals = new Transform[4];
    private readonly float[] spinAngles = new float[4];

    private void Awake()
    {
        Initialize();
        EnsureVisualObjects();
    }

    private void OnEnable()
    {
        Initialize();
        if (Application.isPlaying || createVisualsInEditMode)
        {
            EnsureVisualObjects();
            UpdateVisualTransforms(0f);
        }
    }

    private void Reset()
    {
        Initialize();
        EnsureVisualObjects();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && createVisualsInEditMode)
        {
            Initialize();
            EnsureVisualObjects();
            UpdateVisualTransforms(0f);
        }
    }

    private void LateUpdate()
    {
        UpdateVisualTransforms(Time.deltaTime);
    }

    private void Initialize()
    {
        if (carController == null)
        {
            carController = GetComponent<CarController>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    private void EnsureVisualObjects()
    {
        if (carController == null)
        {
            return;
        }

        if (visualRoot == null)
        {
            Transform existingRoot = transform.Find("WheelVisuals");
            if (existingRoot != null)
            {
                visualRoot = existingRoot;
            }
            else
            {
                GameObject root = new GameObject("WheelVisuals");
                root.transform.SetParent(transform, false);
                visualRoot = root.transform;
            }
        }

        NormalizeVisualRootScale();

        float radius = GetConfiguredWheelRadius(carController.WheelRadius);
        float halfWidth = Mathf.Max(0.02f, wheelWidth * 0.5f);

        for (int i = 0; i < wheelVisuals.Length; i++)
        {
            if (wheelVisuals[i] == null)
            {
                Transform existing = visualRoot.Find(WheelNames[i]);
                if (existing != null)
                {
                    wheelVisuals[i] = existing;
                }
                else
                {
                    GameObject wheelObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    wheelObject.name = WheelNames[i];
                    wheelObject.transform.SetParent(visualRoot, false);
                    wheelVisuals[i] = wheelObject.transform;
                }
            }

            Transform wheelTransform = wheelVisuals[i];
            float diameterScale = radius * 2f;
            wheelTransform.localScale = new Vector3(diameterScale, halfWidth, diameterScale);

            Collider collider = wheelTransform.GetComponent<Collider>();
            if (collider != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(collider);
                }
                else
                {
                    Destroy(collider);
                }
#else
                Destroy(collider);
#endif
            }

            if (wheelMaterial != null)
            {
                MeshRenderer renderer = wheelTransform.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = wheelMaterial;
                }
            }
        }
    }

    private void NormalizeVisualRootScale()
    {
        if (visualRoot == null)
        {
            return;
        }

        Vector3 scale = transform.localScale;
        float x = Mathf.Abs(scale.x) < 0.0001f ? 1f : 1f / scale.x;
        float y = Mathf.Abs(scale.y) < 0.0001f ? 1f : 1f / scale.y;
        float z = Mathf.Abs(scale.z) < 0.0001f ? 1f : 1f / scale.z;
        visualRoot.localScale = new Vector3(x, y, z);
    }

    private void UpdateVisualTransforms(float deltaTime)
    {
        if (carController == null)
        {
            return;
        }

        EnsureVisualObjects();

        for (int i = 0; i < wheelVisuals.Length; i++)
        {
            Transform visual = wheelVisuals[i];
            if (visual == null)
            {
                continue;
            }

            if (!carController.TryGetWheelVisualState(i, out CarController.WheelVisualState wheelState))
            {
                continue;
            }

            Vector3 wheelCenter = wheelState.AnchorPosition - wheelState.SuspensionUp * wheelState.SuspensionLength;
            visual.position = wheelCenter;

            Vector3 axleRight = Vector3.Cross(wheelState.SuspensionUp, wheelState.Forward).normalized;
            Quaternion baseRotation = Quaternion.LookRotation(wheelState.Forward, axleRight);

            float radius = GetConfiguredWheelRadius(wheelState.Radius);
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, wheelState.Forward);
            float wheelAngularSpeedDeg = (forwardSpeed / (2f * Mathf.PI * radius)) * 360f;
            spinAngles[i] += wheelAngularSpeedDeg * deltaTime;

            Quaternion spin = Quaternion.AngleAxis(spinAngles[i], Vector3.up);
            visual.rotation = baseRotation * spin;
        }
    }

    private float GetConfiguredWheelRadius(float physicsRadius)
    {
        if (matchPhysicsWheelRadius)
        {
            return Mathf.Max(0.05f, physicsRadius * Mathf.Max(0.01f, visualRadiusMultiplier));
        }

        return Mathf.Max(0.05f, visualWheelDiameter * 0.5f);
    }
}
