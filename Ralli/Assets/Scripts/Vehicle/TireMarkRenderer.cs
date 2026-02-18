using UnityEngine;

[RequireComponent(typeof(CarController))]
public class TireMarkRenderer : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] [Range(0f, 1f)] private float minSlipIntensity = 0.7f;
    [SerializeField] private float brakeSlipThreshold = 0.8f;

    [Header("Appearance")]
    [SerializeField] private Material tireMarkMaterial;
    [SerializeField] private float markWidth = 0.22f;
    [SerializeField] private float groundOffset = 0.01f;
    [SerializeField] [Range(0f, 1f)] private float maxAlpha = 0.85f;

    [Header("Surface")]
    [Tooltip("Minimum asphalt+gravel weight to emit marks. Below this (e.g. forest floor) no marks appear.")]
    [SerializeField] [Range(0f, 1f)] private float minSurfaceWeight = 0.15f;

    [Header("Mesh Budget")]
    [SerializeField] private int maxQuadsPerWheel = 512;
    [SerializeField] private float minSampleDistance = 0.12f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = ~0;

    private CarController carController;
    private CarInputReader input;
    private WheelMarkState[] wheelStates;
    private Mesh cachedSurfaceMesh;
    private Color[] cachedVertColors;
    private int[] cachedTriangles;

    private class WheelMarkState
    {
        public Vector3[] vertices;
        public Color32[] colors;
        public int[] triangles;
        public int head;
        public int count;
        public Vector3 lastPosition;
        public bool wasEmitting;
        public Mesh mesh;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
    }

    private void Awake()
    {
        carController = GetComponent<CarController>();
        input = GetComponent<CarInputReader>();

        wheelStates = new WheelMarkState[carController.WheelCount];
        for (int i = 0; i < wheelStates.Length; i++)
        {
            wheelStates[i] = CreateWheelState(i);
        }
    }

    private WheelMarkState CreateWheelState(int index)
    {
        var state = new WheelMarkState();
        int vertexCount = (maxQuadsPerWheel + 1) * 2;
        state.vertices = new Vector3[vertexCount];
        state.colors = new Color32[vertexCount];
        state.triangles = new int[maxQuadsPerWheel * 6];
        state.head = 0;
        state.count = 0;
        state.lastPosition = Vector3.zero;
        state.wasEmitting = false;

        state.mesh = new Mesh();
        state.mesh.name = $"TireMarks_Wheel{index}";
        state.mesh.MarkDynamic();

        GameObject meshObj = new GameObject($"TireMarks_Wheel{index}");
        meshObj.transform.SetParent(null);
        meshObj.transform.position = Vector3.zero;
        meshObj.transform.rotation = Quaternion.identity;
        meshObj.transform.localScale = Vector3.one;

        state.meshFilter = meshObj.AddComponent<MeshFilter>();
        state.meshFilter.sharedMesh = state.mesh;

        state.meshRenderer = meshObj.AddComponent<MeshRenderer>();
        state.meshRenderer.sharedMaterial = tireMarkMaterial;
        state.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        state.meshRenderer.receiveShadows = false;

        return state;
    }

    private void OnDestroy()
    {
        if (wheelStates == null) return;

        for (int i = 0; i < wheelStates.Length; i++)
        {
            if (wheelStates[i] == null) continue;

            if (wheelStates[i].meshFilter != null)
            {
                Destroy(wheelStates[i].meshFilter.gameObject);
            }

            if (wheelStates[i].mesh != null)
            {
                Destroy(wheelStates[i].mesh);
            }
        }
    }

    private void LateUpdate()
    {
        for (int i = 0; i < wheelStates.Length; i++)
        {
            UpdateWheel(i, wheelStates[i]);
        }
    }

    private void UpdateWheel(int wheelIndex, WheelMarkState state)
    {
        if (!carController.TryGetWheelVisualState(wheelIndex, out CarController.WheelVisualState visualState))
        {
            state.wasEmitting = false;
            return;
        }

        if (!carController.TryGetWheelTelemetry(wheelIndex, out CarController.WheelTelemetry telemetry))
        {
            state.wasEmitting = false;
            return;
        }

        if (!telemetry.Grounded)
        {
            state.wasEmitting = false;
            return;
        }

        float slipIntensity = GetSlipIntensity(telemetry);
        if (slipIntensity < minSlipIntensity)
        {
            state.wasEmitting = false;
            return;
        }

        // Raycast to sample surface vertex colors at the contact point.
        Vector3 rayOrigin = visualState.AnchorPosition;
        float rayLength = visualState.SuspensionLength + visualState.Radius + 0.2f;
        if (!Physics.Raycast(rayOrigin, -visualState.SuspensionUp, out RaycastHit hit, rayLength, groundMask, QueryTriggerInteraction.Ignore))
        {
            state.wasEmitting = false;
            return;
        }

        // Sample surface type from vertex colors (R=asphalt, G=gravel, B=forest).
        float asphaltWeight = 0f;
        float gravelWeight = 0f;
        if (SampleSurfaceWeights(hit, out float r, out float g, out float b))
        {
            asphaltWeight = r;
            gravelWeight = g;
        }
        else
        {
            // No vertex colors available (e.g. ground plane) — treat as asphalt.
            asphaltWeight = 1f;
        }

        float surfaceWeight = asphaltWeight + gravelWeight;
        if (surfaceWeight < minSurfaceWeight)
        {
            state.wasEmitting = false;
            return;
        }

        Vector3 contactPos = hit.point + hit.normal * groundOffset;

        if (state.wasEmitting)
        {
            float dist = Vector3.Distance(contactPos, state.lastPosition);
            if (dist < minSampleDistance)
            {
                return;
            }
        }

        float alpha = Mathf.Lerp(0f, maxAlpha, Mathf.InverseLerp(minSlipIntensity, 1f, slipIntensity));
        // Vertex color: R = asphalt weight (1=asphalt, 0=gravel), A = opacity.
        float asphaltFraction = surfaceWeight > 0.001f ? asphaltWeight / surfaceWeight : 1f;
        byte rByte = (byte)(Mathf.Clamp01(asphaltFraction) * 255f);
        byte alphaByte = (byte)(Mathf.Clamp01(alpha) * 255f);
        Color32 color = new Color32(rByte, rByte, rByte, alphaByte);

        Vector3 right = telemetry.Right;
        float halfWidth = markWidth * 0.5f;
        Vector3 leftPos = contactPos - right * halfWidth;
        Vector3 rightPos = contactPos + right * halfWidth;

        EmitVertexPair(state, leftPos, rightPos, color);
        state.lastPosition = contactPos;
        state.wasEmitting = true;

        RebuildMesh(state);
    }

    private bool SampleSurfaceWeights(RaycastHit hit, out float r, out float g, out float b)
    {
        r = 0f;
        g = 0f;
        b = 0f;

        MeshCollider meshCollider = hit.collider as MeshCollider;
        if (meshCollider == null) return false;

        Mesh mesh = meshCollider.sharedMesh;
        if (mesh == null) return false;

        int triIndex = hit.triangleIndex;
        if (triIndex < 0) return false;

        if (mesh != cachedSurfaceMesh)
        {
            cachedSurfaceMesh = mesh;
            cachedVertColors = mesh.colors;
            cachedTriangles = mesh.triangles;
        }

        if (cachedVertColors == null || cachedVertColors.Length == 0) return false;

        int i0 = cachedTriangles[triIndex * 3];
        int i1 = cachedTriangles[triIndex * 3 + 1];
        int i2 = cachedTriangles[triIndex * 3 + 2];

        Vector3 bary = hit.barycentricCoordinate;
        Color interpolated = cachedVertColors[i0] * bary.x + cachedVertColors[i1] * bary.y + cachedVertColors[i2] * bary.z;

        r = interpolated.r;
        g = interpolated.g;
        b = interpolated.b;
        return true;
    }

    private float GetSlipIntensity(CarController.WheelTelemetry telemetry)
    {
        if (telemetry.MaxTireForce <= 0f) return 0f;

        Vector2 force = new Vector2(telemetry.LateralForce, telemetry.LongitudinalForce);
        float intensity = force.magnitude / telemetry.MaxTireForce;

        if (input.Brake > brakeSlipThreshold && telemetry.Grounded)
        {
            intensity = Mathf.Max(intensity, input.Brake);
        }

        return Mathf.Clamp01(intensity);
    }

    private void EmitVertexPair(WheelMarkState state, Vector3 left, Vector3 right, Color32 color)
    {
        int maxPairs = maxQuadsPerWheel + 1;
        int pairIndex = state.head;
        int v0 = pairIndex * 2;
        int v1 = v0 + 1;

        state.vertices[v0] = left;
        state.vertices[v1] = right;
        state.colors[v0] = color;
        state.colors[v1] = color;

        state.head = (state.head + 1) % maxPairs;
        if (state.count < maxPairs)
        {
            state.count++;
        }
    }

    private void RebuildMesh(WheelMarkState state)
    {
        if (state.count < 2)
        {
            state.mesh.Clear();
            return;
        }

        int maxPairs = maxQuadsPerWheel + 1;
        int quadCount = state.count - 1;
        int triCount = 0;

        int startPair = (state.head - state.count + maxPairs) % maxPairs;

        for (int i = 0; i < quadCount; i++)
        {
            int currentPair = (startPair + i) % maxPairs;
            int nextPair = (startPair + i + 1) % maxPairs;

            int c0 = currentPair * 2;
            int c1 = c0 + 1;
            int n0 = nextPair * 2;
            int n1 = n0 + 1;

            if (Vector3.SqrMagnitude(state.vertices[n0] - state.vertices[c0]) > 4f)
            {
                continue;
            }

            state.triangles[triCount++] = c0;
            state.triangles[triCount++] = n0;
            state.triangles[triCount++] = n1;

            state.triangles[triCount++] = c0;
            state.triangles[triCount++] = n1;
            state.triangles[triCount++] = c1;
        }

        state.mesh.Clear();
        int vertexCount = (maxQuadsPerWheel + 1) * 2;
        state.mesh.SetVertices(state.vertices, 0, vertexCount);
        state.mesh.SetColors(state.colors, 0, vertexCount);
        state.mesh.SetTriangles(state.triangles, 0, triCount, 0);
        state.mesh.RecalculateBounds();
    }
}
