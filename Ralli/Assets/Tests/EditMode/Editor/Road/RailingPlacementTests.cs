using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RailingPlacementTests
{
    [Test]
    public void ClassifyRailSide_UsesOutsideOfLocalTurn()
    {
        Assert.AreEqual(RailSide.Left, RailPlacementUtility.ClassifySide(0.05f, 0.01f, true, true));
        Assert.AreEqual(RailSide.Right, RailPlacementUtility.ClassifySide(-0.05f, 0.01f, true, true));
        Assert.AreEqual(RailSide.None, RailPlacementUtility.ClassifySide(0.001f, 0.01f, true, true));
    }

    [Test]
    public void ClassifyRailSide_RespectsDesignedPieceToggle()
    {
        Assert.AreEqual(RailSide.None, RailPlacementUtility.ClassifySide(0.05f, 0.01f, false, true));
        Assert.AreEqual(RailSide.Left, RailPlacementUtility.ClassifySide(0.05f, 0.01f, false, false));
    }

    [Test]
    public void BuildRailSpans_MergesContiguousMarks_AndDropsShortSpans()
    {
        List<RailSampleMark> marks = new List<RailSampleMark>
        {
            new RailSampleMark(0f, RailSide.Right),
            new RailSampleMark(2f, RailSide.Right),
            new RailSampleMark(4f, RailSide.None),
            new RailSampleMark(6f, RailSide.Right),
            new RailSampleMark(8f, RailSide.None)
        };

        List<RailSpan> spans = RailPlacementUtility.BuildSpans(marks, 2f);

        Assert.AreEqual(1, spans.Count);
        Assert.AreEqual(RailSide.Right, spans[0].side);
        Assert.That(spans[0].startS, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(spans[0].endS, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void ComputeSignedCurvature_UsesTurnDirectionSign()
    {
        Vector3 prev = Vector3.forward;
        Vector3 nextRightTurn = Quaternion.Euler(0f, 10f, 0f) * Vector3.forward;
        Vector3 nextLeftTurn = Quaternion.Euler(0f, -10f, 0f) * Vector3.forward;

        float rightCurvature = RailPlacementUtility.ComputeSignedCurvature(prev, nextRightTurn, 2f);
        float leftCurvature = RailPlacementUtility.ComputeSignedCurvature(prev, nextLeftTurn, 2f);

        Assert.Greater(rightCurvature, 0f);
        Assert.Less(leftCurvature, 0f);
    }

    [Test]
    public void ComputeEndDrop01_IsZeroAtEnds_AndOneInMiddle()
    {
        float start = 10f;
        float end = 30f;
        float dropDistance = 4f;

        Assert.That(RailPlacementUtility.ComputeEndDrop01(start, start, end, dropDistance), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(RailPlacementUtility.ComputeEndDrop01(end, start, end, dropDistance), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(RailPlacementUtility.ComputeEndDrop01(20f, start, end, dropDistance), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(RailPlacementUtility.ComputeEndDrop01(12f, start, end, dropDistance), Is.EqualTo(0.5f).Within(0.0001f));
    }
}
