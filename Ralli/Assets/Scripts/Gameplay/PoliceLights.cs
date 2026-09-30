using UnityEngine;

// Police van lights: a red/blue roof light bar that glows hard enough for bloom to flare in
// daylight, matching red/blue point lights that wash over the surroundings (and the player's
// cabin up close), and headlights. Strobe pattern per cycle: red double-flash, then blue.
public class PoliceLights : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private PoliceConfig config;
    private Light redLight;
    private Light blueLight;
    private MeshRenderer redBar;
    private MeshRenderer blueBar;
    private MaterialPropertyBlock block;

    // The strobe's current color and brightness (0..1), for effects that flash along with it.
    public Color FlashColor { get; private set; }
    public float FlashAmount { get; private set; }

    public void Initialize(PoliceConfig policeConfig)
    {
        config = policeConfig;
        block = new MaterialPropertyBlock();

        // Parent is scaled to the van size, so work in normalized (-0.5..0.5) local units.
        Vector3 size = config.vanSize;
        float roofY = 0.5f + 0.08f / size.y;
        redBar = CreateBar("LightBar_Red", new Vector3(-0.22f, roofY, 0.3f), size);
        blueBar = CreateBar("LightBar_Blue", new Vector3(0.22f, roofY, 0.3f), size);
        redLight = CreatePointLight("Strobe_Red", new Vector3(-0.22f, roofY + 0.2f / size.y, 0.3f), config.strobeRed);
        blueLight = CreatePointLight("Strobe_Blue", new Vector3(0.22f, roofY + 0.2f / size.y, 0.3f), config.strobeBlue);
        CreateHeadlight(-1f);
        CreateHeadlight(1f);
    }

    private void Update()
    {
        if (config == null)
        {
            return;
        }

        // Cycle: red flash, gap, red flash, gap, blue flash, gap, blue flash, gap.
        float phase = Mathf.Repeat(Time.time * config.strobeFrequency, 1f);
        int slot = Mathf.FloorToInt(phase * 8f);
        bool flashOn = slot % 2 == 0;
        bool red = slot < 4;
        float redAmount = flashOn && red ? 1f : 0f;
        float blueAmount = flashOn && !red ? 1f : 0f;

        redLight.intensity = redAmount * config.strobeLightIntensity;
        blueLight.intensity = blueAmount * config.strobeLightIntensity;
        SetBarGlow(redBar, config.strobeRed, redAmount);
        SetBarGlow(blueBar, config.strobeBlue, blueAmount);

        FlashColor = red ? config.strobeRed : config.strobeBlue;
        FlashAmount = flashOn ? 1f : 0f;
    }

    private MeshRenderer CreateBar(string name, Vector3 localPosition, Vector3 vanSize)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = name;
        Destroy(bar.GetComponent<Collider>());
        bar.transform.SetParent(transform, false);
        bar.transform.localPosition = localPosition;
        bar.transform.localScale = new Vector3(0.55f / vanSize.x, 0.14f / vanSize.y, 0.3f / vanSize.z);

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        MeshRenderer renderer = bar.GetComponent<MeshRenderer>();
        if (unlit != null)
        {
            renderer.sharedMaterial = new Material(unlit) { name = name };
        }

        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    private Light CreatePointLight(string name, Vector3 localPosition, Color color)
    {
        var lightObject = new GameObject(name);
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = localPosition;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = config.strobeLightRange;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        return light;
    }

    private void CreateHeadlight(float side)
    {
        var lightObject = new GameObject(side < 0f ? "Headlight_L" : "Headlight_R");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0.35f * side, -0.15f, 0.5f);
        lightObject.transform.localRotation = Quaternion.Euler(3f, 0f, 0f);
        Light spot = lightObject.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(1f, 0.96f, 0.9f);
        spot.intensity = config.headlightIntensity;
        spot.range = config.headlightRange;
        spot.spotAngle = 55f;
        spot.innerSpotAngle = 30f;
        spot.shadows = LightShadows.None;
    }

    // Dark when off, HDR-bright when on, so bloom flares it.
    private void SetBarGlow(MeshRenderer bar, Color color, float amount)
    {
        Color glow = amount > 0f ? color * config.lightBarEmission : color * 0.15f;
        block.SetColor(BaseColorId, glow);
        bar.SetPropertyBlock(block);
    }
}
