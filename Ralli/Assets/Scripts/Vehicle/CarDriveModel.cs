using UnityEngine;

public static class CarDriveModel
{
    private const float MpsToKph = 3.6f;

    public struct DriveState
    {
        public int GearIndex;
        public float GearRpm01;
        public float ShiftPauseTimer;
        public float BaseDriveForce;
        public float BaseEngineBrakingForce;
    }

    public static bool UpdateReverseState(
        bool inReverse,
        float brakeInput,
        float throttleInput,
        float signedForwardSpeedMps)
    {
        bool nearlyStoppedOrReversing = signedForwardSpeedMps < 0.5f;
        bool nearlyStoppedOrForward = signedForwardSpeedMps > -0.5f;

        if (!inReverse && brakeInput > 0.1f && nearlyStoppedOrReversing)
        {
            return true;
        }

        if (inReverse && throttleInput > 0.1f && nearlyStoppedOrForward)
        {
            return false;
        }

        return inReverse;
    }

    public static float StepBoostFactor(float currentBoostFactor, bool isBoostHeld, CarHandlingConfig handling, float deltaTime)
    {
        if (handling == null)
        {
            return 0f;
        }

        float boostTarget = isBoostHeld ? 1f : 0f;
        float boostRate = isBoostHeld ? handling.boostRampUpSpeed : handling.boostRampDownSpeed;
        return Mathf.MoveTowards(currentBoostFactor, boostTarget, boostRate * deltaTime);
    }

    public static DriveState UpdateDriveState(
        float signedForwardSpeedMps,
        CarHandlingConfig handling,
        int currentGearIndex,
        float currentShiftPauseTimer,
        float deltaTime)
    {
        DriveState result = new DriveState
        {
            GearIndex = 0,
            GearRpm01 = 0f,
            ShiftPauseTimer = 0f,
            BaseDriveForce = 0f,
            BaseEngineBrakingForce = 0f
        };

        if (handling == null)
        {
            return result;
        }

        float speedKph = Mathf.Abs(signedForwardSpeedMps) * MpsToKph;
        float topSpeedKph = Mathf.Max(1f, handling.fakeGearTopSpeedKph);
        float normalizedSpeed = Mathf.Clamp01(speedKph / topSpeedKph);

        int gearCount = Mathf.Max(1, handling.fakeGearCount);
        int gearIndex = GetGearIndex(normalizedSpeed, gearCount, handling.fakeGearThresholds01);
        float rpm01 = GetGearBandProgress(normalizedSpeed, gearIndex, gearCount, handling.fakeGearThresholds01);

        float shiftPauseTimer = currentShiftPauseTimer;
        if (gearIndex != currentGearIndex)
        {
            shiftPauseTimer = Mathf.Max(0.01f, handling.shiftPauseDuration);
        }

        if (shiftPauseTimer > 0f)
        {
            shiftPauseTimer = Mathf.Max(0f, shiftPauseTimer - deltaTime);
        }

        float curveMultiplier = 1f;
        if (handling.baseDriveForceBySpeedKph != null)
        {
            curveMultiplier = Mathf.Max(0f, handling.baseDriveForceBySpeedKph.Evaluate(speedKph));
        }

        float gearShape = Mathf.SmoothStep(1.12f, 0.82f, rpm01);
        float shiftMultiplier = 1f;
        if (shiftPauseTimer > 0f && handling.shiftPauseDuration > 0f)
        {
            float shiftT = 1f - (shiftPauseTimer / handling.shiftPauseDuration);
            float dip = Mathf.Clamp01(handling.shiftTorqueDip);
            shiftMultiplier = Mathf.Lerp(1f - dip, 1f, shiftT);
        }

        result.GearIndex = gearIndex;
        result.GearRpm01 = rpm01;
        result.ShiftPauseTimer = shiftPauseTimer;
        result.BaseDriveForce = handling.maxDriveForce * curveMultiplier * gearShape * shiftMultiplier;
        result.BaseEngineBrakingForce = handling.maxDriveForce * Mathf.Clamp01(handling.engineBrakingStrength) * rpm01;
        return result;
    }

    private static int GetGearIndex(float normalizedSpeed, int gearCount, float[] thresholds01)
    {
        if (gearCount <= 1)
        {
            return 0;
        }

        for (int i = 0; i < gearCount - 1; i++)
        {
            float threshold = GetThreshold(i, gearCount, thresholds01);
            if (normalizedSpeed < threshold)
            {
                return i;
            }
        }

        return gearCount - 1;
    }

    private static float GetGearBandProgress(float normalizedSpeed, int gearIndex, int gearCount, float[] thresholds01)
    {
        if (gearCount <= 1)
        {
            return Mathf.Clamp01(normalizedSpeed);
        }

        float bandStart = gearIndex <= 0 ? 0f : GetThreshold(gearIndex - 1, gearCount, thresholds01);
        float bandEnd = gearIndex >= gearCount - 1 ? 1f : GetThreshold(gearIndex, gearCount, thresholds01);
        float bandLength = Mathf.Max(0.0001f, bandEnd - bandStart);
        return Mathf.Clamp01((normalizedSpeed - bandStart) / bandLength);
    }

    private static float GetThreshold(int index, int gearCount, float[] thresholds01)
    {
        if (thresholds01 == null || thresholds01.Length < gearCount - 1)
        {
            return (index + 1) / (float)gearCount;
        }

        float minAllowed = index <= 0 ? 0f : Mathf.Clamp01(thresholds01[index - 1]);
        float maxAllowed = index >= thresholds01.Length - 1 ? 1f : Mathf.Clamp01(thresholds01[index + 1]);
        float threshold = Mathf.Clamp01(thresholds01[index]);
        return Mathf.Clamp(threshold, minAllowed, maxAllowed);
    }
}
