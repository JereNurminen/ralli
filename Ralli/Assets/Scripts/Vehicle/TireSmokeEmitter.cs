using UnityEngine;

[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(CarInputReader))]
public class TireSmokeEmitter : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private ParticleSystem smokePrefab;

    [Header("Detection")]
    [SerializeField] [Range(0f, 1f)] private float minSlipForSmoke = 0.65f;

    [Header("Emission")]
    [SerializeField] private float maxEmissionRate = 40f;
    [SerializeField] private float emitHeight = 0.1f;

    private CarController carController;
    private CarInputReader input;
    private ParticleSystem[] wheelParticles;

    private void Awake()
    {
        carController = GetComponent<CarController>();
        input = GetComponent<CarInputReader>();

        wheelParticles = new ParticleSystem[carController.WheelCount];
        for (int i = 0; i < wheelParticles.Length; i++)
        {
            wheelParticles[i] = Instantiate(smokePrefab, transform);
            wheelParticles[i].gameObject.name = $"TireSmoke_Wheel{i}";
            wheelParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void LateUpdate()
    {
        for (int i = 0; i < wheelParticles.Length; i++)
            UpdateWheel(i);
    }

    private void UpdateWheel(int wheelIndex)
    {
        ParticleSystem ps = wheelParticles[wheelIndex];
        if (ps == null) return;

        if (!carController.TryGetWheelTelemetry(wheelIndex, out CarController.WheelTelemetry telemetry)
            || !telemetry.Grounded)
        {
            SetEmission(ps, 0f);
            return;
        }

        float slipIntensity = GetSlipIntensity(telemetry);
        if (slipIntensity < minSlipForSmoke)
        {
            SetEmission(ps, 0f);
            return;
        }

        float t = Mathf.InverseLerp(minSlipForSmoke, 1f, slipIntensity);
        float rate = Mathf.Lerp(0f, maxEmissionRate, t);

        ps.transform.position = telemetry.ContactPoint + telemetry.ContactNormal * emitHeight;
        SetEmission(ps, rate);

        if (!ps.isPlaying)
            ps.Play();
    }

    private static void SetEmission(ParticleSystem ps, float rate)
    {
        var emission = ps.emission;
        emission.rateOverTime = rate;

        if (rate <= 0f && ps.isPlaying && ps.particleCount == 0)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private float GetSlipIntensity(CarController.WheelTelemetry telemetry)
    {
        if (telemetry.MaxTireForce <= 0f) return 0f;

        Vector2 force = new Vector2(telemetry.LateralForce, telemetry.LongitudinalForce);
        float intensity = force.magnitude / telemetry.MaxTireForce;

        if (input.Brake > 0.8f && telemetry.Grounded)
            intensity = Mathf.Max(intensity, input.Brake);

        return Mathf.Clamp01(intensity);
    }
}
