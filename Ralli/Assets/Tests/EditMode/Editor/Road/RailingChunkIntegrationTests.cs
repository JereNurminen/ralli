using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RailingChunkIntegrationTests
{
    [Test]
    public void RebuildFromScratch_WithProceduralRailsEnabled_CreatesRailObjects()
    {
        var go = new GameObject("RoadGen");
        var generator = go.AddComponent<RoadStreamGenerator>();
        RoadGenerationConfig cfg = CreateConfig();
        cfg.railsOnlyOnDesignedPieces = false;

        SetPrivateField(generator, "config", cfg);
        SetPrivateField(generator, "generateOnStart", false);
        generator.RebuildFromScratch();

        bool foundRail = HasRailChild(go.transform);
        bool foundRailPost = HasRailPostChild(go.transform);
        Assert.IsTrue(foundRail);
        Assert.IsTrue(foundRailPost);

        Object.DestroyImmediate(cfg);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void RebuildFromScratch_WithDesignedOnlyRails_DoesNotCreateRailsWithoutDesignedPieces()
    {
        var go = new GameObject("RoadGen");
        var generator = go.AddComponent<RoadStreamGenerator>();
        RoadGenerationConfig cfg = CreateConfig();
        cfg.railsOnlyOnDesignedPieces = true;
        cfg.designedPiecePool = null;

        SetPrivateField(generator, "config", cfg);
        SetPrivateField(generator, "generateOnStart", false);
        generator.RebuildFromScratch();

        bool foundRail = HasRailChild(go.transform);
        Assert.IsFalse(foundRail);

        Object.DestroyImmediate(cfg);
        Object.DestroyImmediate(go);
    }

    private static RoadGenerationConfig CreateConfig()
    {
        RoadGenerationConfig cfg = ScriptableObject.CreateInstance<RoadGenerationConfig>();
        cfg.seed = 1337;
        cfg.samplesPerChunk = 24;
        cfg.chunksAhead = 8;
        cfg.chunksBehind = 0;
        cfg.curvePieceProbability = 1f;
        cfg.minCurveLength = 32f;
        cfg.maxCurveLength = 40f;
        cfg.minCurveTurnRateDegPerMeter = 0.3f;
        cfg.maxCurveTurnRateDegPerMeter = 0.3f;
        cfg.maxTurnRateDegPerMeter = 0.3f;
        cfg.minCurvatureForRail = 0.001f;
        cfg.minRailSpanLengthMeters = 4f;
        cfg.railSampleSpacingMeters = 1f;
        cfg.spawnForestTrees = false;
        return cfg;
    }

    private static bool HasRailChild(Transform root)
    {
        foreach (Transform child in root)
        {
            if (child.name.StartsWith("RoadChunk_"))
            {
                foreach (Transform grandChild in child)
                {
                    if (grandChild.name.StartsWith("Rail_"))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasRailPostChild(Transform root)
    {
        foreach (Transform child in root)
        {
            if (child.name.StartsWith("RoadChunk_"))
            {
                foreach (Transform grandChild in child)
                {
                    if (grandChild.name.StartsWith("RailPost_"))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field, $"Field not found: {fieldName}");
        field.SetValue(target, value);
    }
}
