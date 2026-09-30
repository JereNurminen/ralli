using UnityEngine;

// Faintly visible beam for a spot light (fake volumetrics): a cone mesh with an additive haze
// shader that follows the light's color, brightness and on/off state every frame.
[RequireComponent(typeof(Light))]
public class LightCone : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material sharedMaterial;

    [Tooltip("How visible the beam is. Scales with the light's own intensity.")]
    [Range(0f, 1f)] [SerializeField] private float visibility = 0.15f;
    [Tooltip("Beam length as a fraction of the light's range.")]
    [Range(0.1f, 1f)] [SerializeField] private float lengthFraction = 0.6f;
    [SerializeField] private int segments = 24;

    private Light source;
    private MeshRenderer coneRenderer;
    private MaterialPropertyBlock block;

    public void Configure(float coneVisibility, float coneLengthFraction)
    {
        visibility = coneVisibility;
        lengthFraction = coneLengthFraction;
        Rebuild();
    }

    private void Start()
    {
        if (coneRenderer == null)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        source = GetComponent<Light>();
        if (sharedMaterial == null)
        {
            sharedMaterial = Resources.Load<Material>("LightCone");
        }

        if (coneRenderer == null)
        {
            var coneObject = new GameObject("Cone");
            coneObject.transform.SetParent(transform, false);
            coneObject.AddComponent<MeshFilter>();
            coneRenderer = coneObject.AddComponent<MeshRenderer>();
            coneRenderer.sharedMaterial = sharedMaterial;
            coneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coneRenderer.receiveShadows = false;
            block = new MaterialPropertyBlock();
        }

        float length = source.range * lengthFraction;
        coneRenderer.GetComponent<MeshFilter>().sharedMesh = BuildCone(source.spotAngle, length, Mathf.Max(6, segments));
    }

    private void LateUpdate()
    {
        if (coneRenderer == null)
        {
            return;
        }

        bool lit = source.enabled && source.intensity > 0.001f && visibility > 0f;
        coneRenderer.enabled = lit;
        if (!lit)
        {
            return;
        }

        // Light intensities are ~10-60; bring that into a sensible additive brightness.
        block.SetColor(ColorId, source.color * (source.intensity * visibility * 0.02f));
        coneRenderer.SetPropertyBlock(block);
    }

    // Open cone along +Z with its apex at the light; uv.y runs 0 (apex) to 1 (far end).
    private static Mesh BuildCone(float spotAngle, float length, int segments)
    {
        float radius = Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad) * length;
        var vertices = new Vector3[(segments + 1) * 2];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 normal = (radial * length - Vector3.forward * radius).normalized;
            vertices[i * 2] = Vector3.zero;
            vertices[i * 2 + 1] = radial * radius + Vector3.forward * length;
            normals[i * 2] = normal;
            normals[i * 2 + 1] = normal;
            uvs[i * 2] = new Vector2(i / (float)segments, 0f);
            uvs[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            int t = i * 6;
            int a = i * 2;
            triangles[t] = a;
            triangles[t + 1] = a + 1;
            triangles[t + 2] = a + 3;
            triangles[t + 3] = a;
            triangles[t + 4] = a + 3;
            triangles[t + 5] = a + 2;
        }

        var mesh = new Mesh { name = "LightCone", vertices = vertices, normals = normals, uv = uvs, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }
}
