using UnityEngine;

public static class CarSteeringModel
{
    private const float MpsToKph = 3.6f;

    public static float EvaluateSteerFactor(float signedForwardSpeedMps, CarHandlingConfig handling)
    {
        if (handling == null || handling.steerFadeSpeedKph <= 0.01f)
        {
            return 1f;
        }

        float speedKph = Mathf.Abs(signedForwardSpeedMps) * MpsToKph;
        float fadeT = Mathf.Clamp01(speedKph / handling.steerFadeSpeedKph);
        return Mathf.Lerp(1f, handling.highSpeedSteerFactor, fadeT);
    }

    public static float StepSteerAngle(
        float currentSteerAngle,
        float steerInput,
        float steerFactor,
        CarHandlingConfig handling,
        float deltaTime)
    {
        if (handling == null)
        {
            return 0f;
        }

        float targetSteerAngle = steerInput * handling.maxSteerAngle * steerFactor;
        float nextSteerAngle = Mathf.MoveTowards(
            currentSteerAngle,
            targetSteerAngle,
            handling.steerResponse * handling.maxSteerAngle * deltaTime
        );

        if (Mathf.Abs(steerInput) < 0.001f && Mathf.Abs(nextSteerAngle) < 0.05f)
        {
            return 0f;
        }

        return nextSteerAngle;
    }

    public static float ApplyFrontWheelAuthority(float steerAngle, int groundedFrontWheels, CarHandlingConfig handling)
    {
        if (handling == null)
        {
            return steerAngle;
        }

        float frontGroundFactor = Mathf.Clamp01(groundedFrontWheels / 2f);
        float steerAuthority = Mathf.Lerp(handling.steerWhenFrontAirborne, 1f, frontGroundFactor);
        return steerAngle * steerAuthority;
    }
}
