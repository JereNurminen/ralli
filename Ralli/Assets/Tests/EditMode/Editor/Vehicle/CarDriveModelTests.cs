using NUnit.Framework;
using UnityEngine;

public class CarDriveModelTests
{
    private CarHandlingConfig CreateConfig()
    {
        CarHandlingConfig config = ScriptableObject.CreateInstance<CarHandlingConfig>();
        config.boostRampUpSpeed = 8f;
        config.boostRampDownSpeed = 4f;
        config.fakeGearTopSpeedKph = 200f;
        config.fakeGearCount = 5;
        config.maxDriveForce = 7000f;
        config.engineBrakingStrength = 0.2f;
        config.shiftPauseDuration = 0.08f;
        config.shiftTorqueDip = 0.75f;
        config.baseDriveForceBySpeedKph = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(200f, 0.5f)
        );
        return config;
    }

    [Test]
    public void UpdateReverseState_EntersReverseWhenBrakingNearStop()
    {
        bool reverse = CarDriveModel.UpdateReverseState(false, brakeInput: 1f, throttleInput: 0f, signedForwardSpeedMps: 0.1f);
        Assert.That(reverse, Is.True);
    }

    [Test]
    public void UpdateReverseState_ExitsReverseWhenThrottleNearStop()
    {
        bool reverse = CarDriveModel.UpdateReverseState(true, brakeInput: 0f, throttleInput: 1f, signedForwardSpeedMps: -0.1f);
        Assert.That(reverse, Is.False);
    }

    [Test]
    public void StepBoostFactor_RampsUpAndDown()
    {
        CarHandlingConfig config = CreateConfig();

        float up = CarDriveModel.StepBoostFactor(0f, true, config, 0.1f);
        float down = CarDriveModel.StepBoostFactor(1f, false, config, 0.1f);

        Assert.That(up, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(down, Is.EqualTo(0.6f).Within(0.0001f));
    }

    [Test]
    public void UpdateDriveState_UsesThresholdsToSelectGear()
    {
        CarHandlingConfig config = CreateConfig();
        config.fakeGearThresholds01 = new[] { 0.2f, 0.4f, 0.7f, 0.9f };

        CarDriveModel.DriveState state = CarDriveModel.UpdateDriveState(
            signedForwardSpeedMps: (0.65f * config.fakeGearTopSpeedKph) / 3.6f,
            handling: config,
            currentGearIndex: 0,
            currentShiftPauseTimer: 0f,
            deltaTime: 0.02f
        );

        Assert.That(state.GearIndex, Is.EqualTo(2));
        Assert.That(state.GearRpm01, Is.GreaterThan(0f));
        Assert.That(state.GearRpm01, Is.LessThan(1f));
    }
}
