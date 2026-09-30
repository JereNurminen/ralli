using UnityEngine;

// Fake vehicle lights: glowing headlight and tail light faces on a box-shaped body, each with a
// GPU-billboarded flare, and tail lights that brighten while braking. No real light sources, so
// they're cheap enough for every car on the road. Materials are shared across all vehicles.
public class VehicleLights : MonoBehaviour
{
    private static Material headFaceMaterial;
    private static Material tailFaceMaterial;
    private static Material brakeFaceMaterial;
    private static Material headFlareMaterial;
    private static Material tailFlareMaterial;
    private static Material brakeFlareMaterial;
    private static VehicleLightsConfig materialsConfig;

    private MeshRenderer[] tailFaces;
    private MeshRenderer[] tailFlares;
    private bool braking;

    // bodySize: the body box (width, height, length) in meters. scaledParent: true when this object
    // is itself scaled to the body size (then children are placed in normalized units).
    public void Build(VehicleLightsConfig config, Vector3 bodySize, bool scaledParent)
    {
        EnsureMaterials(config);
        Vector3 unit = scaledParent ? new Vector3(1f / bodySize.x, 1f / bodySize.y, 1f / bodySize.z) : Vector3.one;
        float y = -bodySize.y * 0.5f + config.lightHeight;
        float x = bodySize.x * 0.5f - config.sideInset;
        float front = bodySize.z * 0.5f + 0.01f;
        float back = -front;

        tailFaces = new MeshRenderer[2];
        tailFlares = new MeshRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            CreateFace("Headlight", new Vector3(x * side, y, front), config.headlightSize, false, headFaceMaterial, unit);
            CreateFlare("HeadlightFlare", new Vector3(x * side, y, front), false, headFlareMaterial, unit);
            tailFaces[i] = CreateFace("TailLight", new Vector3(x * side, y, back), config.tailLightSize, true, tailFaceMaterial, unit);
            tailFlares[i] = CreateFlare("TailLightFlare", new Vector3(x * side, y, back), true, tailFlareMaterial, unit);
        }
    }

    public void SetBraking(bool isBraking)
    {
        if (tailFaces == null || isBraking == braking)
        {
            return;
        }

        braking = isBraking;
        for (int i = 0; i < tailFaces.Length; i++)
        {
            tailFaces[i].sharedMaterial = braking ? brakeFaceMaterial : tailFaceMaterial;
            tailFlares[i].sharedMaterial = braking ? brakeFlareMaterial : tailFlareMaterial;
        }
    }

    private MeshRenderer CreateFace(string name, Vector3 position, Vector2 size, bool facingBack, Material material, Vector3 unit)
    {
        MeshRenderer face = CreateQuad(name, position, facingBack, material, unit);
        face.transform.localScale = Vector3.Scale(new Vector3(size.x, size.y, 1f), unit);
        return face;
    }

    private MeshRenderer CreateFlare(string name, Vector3 position, bool facingBack, Material material, Vector3 unit)
    {
        // Scale is irrelevant to the flare shader (it sizes itself); only position and facing matter.
        // The shader treats the flare's +Z as the light's direction.
        MeshRenderer flare = CreateQuad(name, position, facingBack, material, unit);
        flare.transform.localRotation = Quaternion.Euler(0f, facingBack ? 180f : 0f, 0f);
        return flare;
    }

    private MeshRenderer CreateQuad(string name, Vector3 position, bool facingBack, Material material, Vector3 unit)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(transform, false);
        quad.transform.localPosition = Vector3.Scale(position, unit);
        // Quads face -Z; light faces point out of the body (+Z at the front, -Z at the back).
        quad.transform.localRotation = Quaternion.Euler(0f, facingBack ? 0f : 180f, 0f);

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static void EnsureMaterials(VehicleLightsConfig config)
    {
        if (materialsConfig == config && headFaceMaterial != null)
        {
            return;
        }

        materialsConfig = config;
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        headFaceMaterial = CreateFaceMaterial(unlit, config.headlightColor * config.headlightEmission);
        tailFaceMaterial = CreateFaceMaterial(unlit, config.tailLightColor * config.tailLightEmission);
        brakeFaceMaterial = CreateFaceMaterial(unlit, config.tailLightColor * config.brakeLightEmission);

        Material flareBase = Resources.Load<Material>("LightFlare");
        headFlareMaterial = CreateFlareMaterial(flareBase, config.headlightColor * config.headlightFlareIntensity, config.headlightFlareSize, config.headlightFlareAngularSize);
        tailFlareMaterial = CreateFlareMaterial(flareBase, config.tailLightColor * config.tailFlareIntensity, config.tailFlareSize, config.tailFlareAngularSize);
        brakeFlareMaterial = CreateFlareMaterial(flareBase, config.tailLightColor * config.brakeFlareIntensity, config.tailFlareSize * 1.3f, config.tailFlareAngularSize * 1.3f);
    }

    private static Material CreateFaceMaterial(Shader unlit, Color glow)
    {
        var material = new Material(unlit);
        material.SetColor("_BaseColor", glow);
        return material;
    }

    private static Material CreateFlareMaterial(Material flareBase, Color color, float size, float angularSize)
    {
        var material = new Material(flareBase);
        material.SetColor("_Color", color);
        material.SetFloat("_BaseSize", size);
        material.SetFloat("_AngularSize", angularSize);
        return material;
    }
}
