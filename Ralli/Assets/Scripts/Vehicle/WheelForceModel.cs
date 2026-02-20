using UnityEngine;

public static class WheelForceModel
{
    public static float EvaluateSlipCurve(AnimationCurve curve, float normalizedSlip)
    {
        if (curve == null)
        {
            return 1f;
        }

        return Mathf.Clamp01(curve.Evaluate(normalizedSlip));
    }

    public static Vector2 ClampToFrictionCircle(
        Vector2 requestedForce,
        float maxTireForce,
        bool prioritizeLongitudinal,
        float longitudinalPriorityBlend)
    {
        if (maxTireForce <= 0f)
        {
            return Vector2.zero;
        }

        if (requestedForce.magnitude <= maxTireForce)
        {
            return requestedForce;
        }

        if (!prioritizeLongitudinal || longitudinalPriorityBlend <= 0.01f)
        {
            return requestedForce.normalized * maxTireForce;
        }

        float absLongitudinal = Mathf.Abs(requestedForce.y);
        float clampedLongitudinal = Mathf.Min(absLongitudinal, maxTireForce);
        float lateralBudget = Mathf.Sqrt(Mathf.Max(0f, maxTireForce * maxTireForce - clampedLongitudinal * clampedLongitudinal));

        Vector2 normalClamped = requestedForce.normalized * maxTireForce;
        Vector2 longitudinalPriorityClamped = new Vector2(
            Mathf.Sign(requestedForce.x) * Mathf.Min(Mathf.Abs(requestedForce.x), lateralBudget),
            Mathf.Sign(requestedForce.y) * clampedLongitudinal
        );

        return Vector2.Lerp(normalClamped, longitudinalPriorityClamped, Mathf.Clamp01(longitudinalPriorityBlend));
    }
}
