using UnityEngine;

// Police van lights: red and blue rotating beacons on the roof, each sweeping a spot-light beam
// (with a faintly visible cone) around the van, half a turn apart. The roof light bar flares
// whenever a beam sweeps past the viewer; dim red/blue fill lights tint the surroundings between
// sweeps. Also headlights. The van root is unscaled, so everything here is in meters.
public class PoliceLights : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private PoliceConfig config;
    private Transform redBeacon;
    private Transform blueBeacon;
    private Light redFill;
    private Light blueFill;
    private MeshRenderer redBar;
    private MeshRenderer blueBar;
    private MaterialPropertyBlock block;
    private float spin;

    // How strongly a beam currently faces the main camera (0..1), and that beam's color, for
    // effects that pulse along with the sweeps.
    public Color FlashColor { get; private set; }
    public float FlashAmount { get; private set; }

    public void Initialize(PoliceConfig policeConfig)
    {
        config = policeConfig;
        block = new MaterialPropertyBlock();

        float roof = config.vanSize.y * 0.5f;
        float front = config.vanSize.z * 0.5f;
        redBar = CreateBar("LightBar_Red", new Vector3(-0.42f, roof + 0.08f, 0.6f));
        blueBar = CreateBar("LightBar_Blue", new Vector3(0.42f, roof + 0.08f, 0.6f));
        redBeacon = CreateBeacon("Beacon_Red", new Vector3(-0.42f, roof + 0.12f, 0.6f), config.strobeRed);
        blueBeacon = CreateBeacon("Beacon_Blue", new Vector3(0.42f, roof + 0.12f, 0.6f), config.strobeBlue);
        redFill = CreateFill("Fill_Red", new Vector3(-0.42f, roof + 0.3f, 0.6f), config.strobeRed);
        blueFill = CreateFill("Fill_Blue", new Vector3(0.42f, roof + 0.3f, 0.6f), config.strobeBlue);
        CreateHeadlight(-1f, front);
        CreateHeadlight(1f, front);
    }

    private void Update()
    {
        if (config == null)
        {
            return;
        }

        spin = Mathf.Repeat(spin + config.beaconTurnsPerSecond * 360f * Time.deltaTime, 360f);
        redBeacon.localRotation = Quaternion.Euler(4f, spin, 0f);
        blueBeacon.localRotation = Quaternion.Euler(4f, spin + 180f, 0f);

        // Each bar flares as its beam sweeps past the camera, like a real rotating beacon.
        float redFacing = GetFacing(redBeacon);
        float blueFacing = GetFacing(blueBeacon);
        SetBarGlow(redBar, config.strobeRed, redFacing);
        SetBarGlow(blueBar, config.strobeBlue, blueFacing);

        float fill = config.beaconIntensity * config.beaconFillAmount;
        redFill.intensity = fill;
        blueFill.intensity = fill;

        bool redStronger = redFacing >= blueFacing;
        FlashColor = redStronger ? config.strobeRed : config.strobeBlue;
        FlashAmount = redStronger ? redFacing : blueFacing;
    }

    // 1 when the beam points straight at the camera, falling off sharply as it sweeps away.
    private static float GetFacing(Transform beacon)
    {
        Camera viewer = Camera.main;
        if (viewer == null)
        {
            return 0f;
        }

        Vector3 toViewer = Vector3.ProjectOnPlane(viewer.transform.position - beacon.position, beacon.parent.up);
        Vector3 beam = Vector3.ProjectOnPlane(beacon.forward, beacon.parent.up);
        float alignment = Mathf.Max(0f, Vector3.Dot(beam.normalized, toViewer.normalized));
        return Mathf.Pow(alignment, 8f);
    }

    private MeshRenderer CreateBar(string name, Vector3 localPosition)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = name;
        Destroy(bar.GetComponent<Collider>());
        bar.transform.SetParent(transform, false);
        bar.transform.localPosition = localPosition;
        bar.transform.localScale = new Vector3(0.55f, 0.14f, 0.3f);

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        MeshRenderer renderer = bar.GetComponent<MeshRenderer>();
        if (unlit != null)
        {
            renderer.sharedMaterial = new Material(unlit) { name = name };
        }

        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    private Transform CreateBeacon(string name, Vector3 localPosition, Color color)
    {
        var beacon = new GameObject(name);
        beacon.transform.SetParent(transform, false);
        beacon.transform.localPosition = localPosition;
        Light beam = beacon.AddComponent<Light>();
        beam.type = LightType.Spot;
        beam.color = color;
        beam.intensity = config.beaconIntensity;
        beam.range = config.beaconRange;
        beam.spotAngle = config.beaconBeamAngle;
        beam.innerSpotAngle = config.beaconBeamAngle * 0.5f;
        beam.shadows = LightShadows.None;
        beacon.AddComponent<LightCone>().Configure(config.beamVisibility, 0.7f);
        return beacon.transform;
    }

    private Light CreateFill(string name, Vector3 localPosition, Color color)
    {
        var fillObject = new GameObject(name);
        fillObject.transform.SetParent(transform, false);
        fillObject.transform.localPosition = localPosition;
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.color = color;
        fill.range = config.beaconRange * 0.5f;
        fill.shadows = LightShadows.None;
        return fill;
    }

    private void CreateHeadlight(float side, float front)
    {
        var lightObject = new GameObject(side < 0f ? "Headlight_L" : "Headlight_R");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0.65f * side, -0.3f, front);
        lightObject.transform.localRotation = Quaternion.Euler(3f, 0f, 0f);
        Light spot = lightObject.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(1f, 0.96f, 0.9f);
        spot.intensity = config.headlightIntensity;
        spot.range = config.headlightRange;
        spot.spotAngle = 55f;
        spot.innerSpotAngle = 30f;
        spot.shadows = LightShadows.None;
        lightObject.AddComponent<LightCone>().Configure(config.beamVisibility * 0.5f, 0.5f);
    }

    // Dim at rest, HDR-bright as a beam faces the viewer, so bloom flares it.
    private void SetBarGlow(MeshRenderer bar, Color color, float amount)
    {
        Color glow = color * Mathf.Lerp(0.3f, config.lightBarEmission, amount);
        block.SetColor(BaseColorId, glow);
        bar.SetPropertyBlock(block);
    }
}
