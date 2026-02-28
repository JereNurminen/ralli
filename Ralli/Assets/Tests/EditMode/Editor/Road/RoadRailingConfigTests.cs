using NUnit.Framework;
using UnityEngine;

public class RoadRailingConfigTests
{
    [Test]
    public void RoadGenerationConfig_RailingDefaults_AreValid()
    {
        RoadGenerationConfig cfg = ScriptableObject.CreateInstance<RoadGenerationConfig>();

        Assert.IsTrue(cfg.railsOnlyOnDesignedPieces);
        Assert.Greater(cfg.minCurvatureForRail, 0f);
        Assert.Greater(cfg.minRailSpanLengthMeters, 0f);
        Assert.Greater(cfg.railSampleSpacingMeters, 0f);
        Assert.Greater(cfg.railEndDropDistanceMeters, 0f);
        Assert.Greater(cfg.railBeamDepthMeters, 0f);
        Assert.Greater(cfg.railBeamHeightMeters, 0f);
        Assert.Greater(cfg.railBeamFlangeThicknessMeters, 0f);
        Assert.Greater(cfg.railPostSpacingMeters, 0f);
        Assert.Greater(cfg.railPostWidthMeters, 0f);
        Assert.Greater(cfg.railPostDepthMeters, 0f);
        Assert.Greater(cfg.railPostHeightMeters, 0f);
    }
}
