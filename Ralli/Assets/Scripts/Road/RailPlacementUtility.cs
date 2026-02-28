using System.Collections.Generic;
using UnityEngine;

public enum RailSide
{
    None = 0,
    Left = 1,
    Right = 2
}

public struct RailSampleMark
{
    public readonly float s;
    public readonly RailSide side;

    public RailSampleMark(float s, RailSide side)
    {
        this.s = s;
        this.side = side;
    }
}

public struct RailSpan
{
    public float startS;
    public float endS;
    public RailSide side;
}

public static class RailPlacementUtility
{
    public static float ComputeSignedCurvature(Vector3 prevForward, Vector3 nextForward, float distanceMeters)
    {
        Vector3 a = new Vector3(prevForward.x, 0f, prevForward.z).normalized;
        Vector3 b = new Vector3(nextForward.x, 0f, nextForward.z).normalized;
        if (a.sqrMagnitude < 0.0001f || b.sqrMagnitude < 0.0001f || distanceMeters <= 0.0001f)
        {
            return 0f;
        }

        float signedAngleRad = Vector3.SignedAngle(a, b, Vector3.up) * Mathf.Deg2Rad;
        return signedAngleRad / distanceMeters;
    }

    public static RailSide ClassifySide(float signedCurvature, float minCurvatureForRail, bool isDesignedPiece, bool railsOnlyOnDesignedPieces)
    {
        if (railsOnlyOnDesignedPieces && !isDesignedPiece)
        {
            return RailSide.None;
        }

        if (Mathf.Abs(signedCurvature) < Mathf.Max(0f, minCurvatureForRail))
        {
            return RailSide.None;
        }

        return signedCurvature > 0f ? RailSide.Left : RailSide.Right;
    }

    public static List<RailSpan> BuildSpans(List<RailSampleMark> marks, float minSpanLengthMeters)
    {
        var spans = new List<RailSpan>();
        if (marks == null || marks.Count == 0)
        {
            return spans;
        }

        float minLength = Mathf.Max(0f, minSpanLengthMeters);
        RailSide currentSide = RailSide.None;
        float spanStartS = 0f;
        float spanEndS = 0f;

        for (int i = 0; i < marks.Count; i++)
        {
            RailSampleMark mark = marks[i];
            if (mark.side == RailSide.None)
            {
                TryAddSpan(spans, currentSide, spanStartS, spanEndS, minLength);
                currentSide = RailSide.None;
                continue;
            }

            if (currentSide == RailSide.None)
            {
                currentSide = mark.side;
                spanStartS = mark.s;
                spanEndS = mark.s;
                continue;
            }

            if (mark.side != currentSide)
            {
                TryAddSpan(spans, currentSide, spanStartS, spanEndS, minLength);
                currentSide = mark.side;
                spanStartS = mark.s;
                spanEndS = mark.s;
                continue;
            }

            spanEndS = mark.s;
        }

        TryAddSpan(spans, currentSide, spanStartS, spanEndS, minLength);
        return spans;
    }

    public static float ComputeEndDrop01(float s, float startS, float endS, float dropDistanceMeters)
    {
        float dropDistance = Mathf.Max(0f, dropDistanceMeters);
        if (dropDistance <= 0.0001f || endS <= startS)
        {
            return 1f;
        }

        float distanceToEdge = Mathf.Min(s - startS, endS - s);
        return Mathf.Clamp01(distanceToEdge / dropDistance);
    }

    private static void TryAddSpan(List<RailSpan> spans, RailSide side, float startS, float endS, float minLength)
    {
        if (side == RailSide.None)
        {
            return;
        }

        float length = endS - startS;
        if (length < minLength)
        {
            return;
        }

        spans.Add(new RailSpan
        {
            startS = startS,
            endS = endS,
            side = side
        });
    }
}
