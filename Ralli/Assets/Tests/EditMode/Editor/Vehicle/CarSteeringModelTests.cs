using NUnit.Framework;
using UnityEngine;

public class CarSteeringModelTests
{
    private CarHandlingConfig CreateConfig()
    {
        CarHandlingConfig config = ScriptableObject.CreateInstance<CarHandlingConfig>();
        config.steerFadeSpeedKph = 100f;
        config.highSpeedSteerFactor = 0.5f;
        config.maxSteerAngle = 30f;
        config.steerResponse = 10f;
        config.steerWhenFrontAirborne = 0.2f;
        return config;
    }

    [Test]
    public void EvaluateSteerFactor_FadesWithSpeed()
    {
        CarHandlingConfig config = CreateConfig();

        float lowSpeedFactor = CarSteeringModel.EvaluateSteerFactor(0f, config);
        float highSpeedFactor = CarSteeringModel.EvaluateSteerFactor(100f / 3.6f, config);

        Assert.That(lowSpeedFactor, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(highSpeedFactor, Is.EqualTo(0.5f).Within(0.0001f));
    }

    [Test]
    public void ApplyFrontWheelAuthority_ReducesSteeringWhenAirborne()
    {
        CarHandlingConfig config = CreateConfig();

        float noFrontGround = CarSteeringModel.ApplyFrontWheelAuthority(20f, 0, config);
        float bothFrontGrounded = CarSteeringModel.ApplyFrontWheelAuthority(20f, 2, config);

        Assert.That(noFrontGround, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(bothFrontGrounded, Is.EqualTo(20f).Within(0.0001f));
    }
}
