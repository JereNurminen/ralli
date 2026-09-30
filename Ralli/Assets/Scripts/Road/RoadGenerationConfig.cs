using UnityEngine;

[CreateAssetMenu(menuName = "Ralli/Road/Road Generation Config", fileName = "RoadGenerationConfig")]
public class RoadGenerationConfig : ScriptableObject
{
    [Header("Determinism")]
    [Tooltip("Seed for deterministic road generation. 0 = pick a random 6-digit seed on every (re)build.")]
    public int seed = 1337;

    [Header("Chunking")]
    [Tooltip("Road chunk length in meters.")]
    public float chunkLength = 140f;
    [Tooltip("Minimum chunk length in meters. If <= 0, chunkLength is used.")]
    public float minChunkLength = 100f;
    [Tooltip("Maximum chunk length in meters. If <= 0, chunkLength is used.")]
    public float maxChunkLength = 180f;
    [Tooltip("Samples per chunk. Higher = smoother mesh, more vertices.")]
    public int samplesPerChunk = 80;
    [Tooltip("How many chunks to keep generated ahead of player chunk.")]
    public int chunksAhead = 8;
    [Tooltip("How many chunks to keep behind player chunk.")]
    public int chunksBehind = 2;

    [Header("Road Shape")]
    [Tooltip("Asphalt width in meters.")]
    public float roadWidth = 8f;
    [Tooltip("Road mesh thickness in meters.")]
    public float roadThickness = 0.35f;
    [Tooltip("Shoulder width on each side (dirt band) in meters.")]
    public float shoulderWidth = 1.2f;
    [Tooltip("Shoulder vertical drop from asphalt in meters.")]
    public float shoulderDrop = 0.05f;
    [Tooltip("Ditch width on each side beyond shoulder in meters.")]
    public float ditchWidth = 2.0f;
    [Tooltip("Ditch depth below shoulder level in meters.")]
    public float ditchDepth = 0.55f;
    [Tooltip("Flat bottom width of the ditch in meters (0 = V-shaped).")]
    public float ditchBottomFlatWidth = 0f;
    [Tooltip("Ditch outer lip height relative to road centerline Y in meters (negative = lower). Terrain meets the road here.")]
    public float forestFloorYOffset = -0.05f;
    [Tooltip("Short downward apron below the ditch outer lip (m). Hides small gaps between road mesh and terrain.")]
    public float corridorApronDepth = 1.5f;
    [Tooltip("Min curve radius = corridor half-width x this. Guarantees the road corridor mesh never folds on tight turns.")]
    public float corridorRadiusMargin = 1.5f;
    [Header("Forest Trees")]
    [Tooltip("Enable procedural tree spawning on the forest floor.")]
    public bool spawnForestTrees = true;
    [Tooltip("Trees spawn this far beyond the ditch clearance (m). Beyond it there is bare terrain for fog to hide.")]
    public float treeBandWidth = 45f;
    [Tooltip("Tree grid cell size (m). One tree candidate per cell, jittered inside it.")]
    public float treeCellSize = 5f;
    [Tooltip("Chance a grid cell gets a tree.")]
    [Range(0f, 1f)] public float treeDensity = 0.6f;
    [Tooltip("Birch-to-pine mix. 1 = all birch, 0 = all pine.")]
    [Range(0f, 1f)] public float birchRatio = 0.65f;
    [Tooltip("Extra lateral clearance from ditch outer edge before trees may spawn (meters).")]
    public float treeDitchClearance = 1.5f;
    [Tooltip("Trunk collider diameter in meters.")]
    public float treeColliderWidth = 0.6f;
    [Tooltip("Trunk collider height in meters.")]
    public float treeColliderHeight = 8f;
    [Tooltip("Base rotation offset applied to tree models before random yaw.")]
    public Vector3 treeModelRotationOffsetEuler = new Vector3(-90f, 0f, 0f);
    [Tooltip("Max heading change rate in deg/m.")]
    public float maxTurnRateDegPerMeter = 0.22f;
    [Tooltip("How quickly turn rate moves toward piece target (0..1 per sample).")]
    [Range(0.01f, 1f)] public float turnRateResponse = 0.08f;

    [Header("Elevation")]
    [Tooltip("Road follows terrain height and gets short bumps. Off = flat road (terrain still has relief).")]
    public bool enableHills = true;
    [Tooltip("Short bump amplitude in meters.")]
    public float smallBumpAmplitude = 0.9f;
    [Tooltip("Short bump wavelength in meters.")]
    public float smallBumpWavelength = 42f;
    [Tooltip("How often short bump patches appear along the road (0..1).")]
    [Range(0f, 1f)] public float smallBumpOccurrence = 0.28f;
    [Tooltip("Typical length of bump/no-bump patches in meters.")]
    public float smallBumpPatchLength = 140f;
    [Tooltip("Distance (m) over which the road climbs/descends to catch up with the terrain height.")]
    public float elevationCatchupDistance = 30f;
    [Tooltip("Distance (m) over which the height offset left by a designed elevation piece fades back to terrain height.")]
    public float designedElevationOffsetFade = 400f;
    [Tooltip("Maximum road grade angle in degrees.")]
    public float maxSlopeAngleDeg = 8f;
    [Tooltip("How quickly slope follows target elevation change (0..1 per sample).")]
    [Range(0.01f, 1f)] public float slopeResponse = 0.12f;

    [Header("Terrain")]
    [Tooltip("Terrain tile size (m).")]
    public float terrainTileSize = 48f;
    [Tooltip("Quads per tile side. Tile size / this = vertex spacing.")]
    public int terrainTileResolution = 24;
    [Tooltip("Terrain is streamed along the road up to this distance from the stream center (m).")]
    public float terrainRadius = 720f;
    [Tooltip("Gap (m) between the outermost trees and the wall at the terrain band edge. The band itself ends where tree coverage ends.")]
    public float terrainWallMargin = 2f;
    [Tooltip("Height of the wall at the terrain band edge (m).")]
    public float terrainWallHeight = 25f;
    [Tooltip("How far the wall extends below ground (m), so slopes never show a gap under it.")]
    public float terrainWallSink = 3f;
    [Tooltip("Stream center is shifted this far (m) from the car toward the target bearing.")]
    public float terrainBearingBias = 60f;
    [Tooltip("Max tiles built per frame. Limits hitches.")]
    public int terrainTilesPerFrame = 4;
    [Tooltip("Base wavelength of terrain relief (m).")]
    public float terrainNoiseScale = 260f;
    [Tooltip("Terrain relief amplitude (m).")]
    public float terrainNoiseAmplitude = 16f;
    [Tooltip("Noise octaves. Each adds half the amplitude at double the frequency.")]
    [Range(1, 5)] public int terrainNoiseOctaves = 3;
    [Tooltip("Width (m) beyond the road corridor where terrain blends from road height to its own height (cut/fill zone).")]
    public float roadBlendWidth = 25f;
    [Tooltip("Flat ring (m) at road lip height right outside the corridor before the blend starts.")]
    public float roadFlatRing = 1.5f;
    [Tooltip("How far (m) terrain sits below the ditch lip under the road corridor, so it never pokes through.")]
    public float corridorTuckDepth = 1.2f;

    [Header("Road Pieces")]
    [Tooltip("Chance that the next piece is a curve instead of a straight.")]
    [Range(0f, 1f)] public float curvePieceProbability = 0.72f;
    [Tooltip("Minimum straight piece length in meters.")]
    public float minStraightLength = 28f;
    [Tooltip("Maximum straight piece length in meters.")]
    public float maxStraightLength = 90f;
    [Tooltip("Minimum curve piece length in meters.")]
    public float minCurveLength = 30f;
    [Tooltip("Maximum curve piece length in meters.")]
    public float maxCurveLength = 75f;
    [Tooltip("Minimum absolute curve turn rate in deg/m.")]
    public float minCurveTurnRateDegPerMeter = 0.12f;
    [Tooltip("Maximum absolute curve turn rate in deg/m.")]
    public float maxCurveTurnRateDegPerMeter = 0.30f;
    [Tooltip("Chance to flip curve direction vs previous curve.")]
    [Range(0f, 1f)] public float oppositeCurveChance = 0.65f;

    [Header("Designed Road Pieces")]
    [Tooltip("Pool of hand-authored road pieces to inject. Leave null to disable.")]
    public DesignedRoadPiecePool designedPiecePool;
    [Tooltip("Minimum procedural distance between designed pieces (meters).")]
    public float minProceduralBetweenDesigned = 200f;
    [Tooltip("Maximum procedural distance between designed pieces (meters).")]
    public float maxProceduralBetweenDesigned = 600f;
    [Tooltip("How strongly designed pieces and procedural curve directions correct heading drift (0 = random, 1 = strong).")]
    [Range(0f, 1f)] public float headingCorrectionStrength = 0.4f;
    [Tooltip("Target average heading in degrees (0 = north/forward).")]
    public float targetBearing = 0f;

    [Header("Banking")]
    [Tooltip("How strongly curvature affects banking.")]
    public float bankFromCurvature = 8f;
    [Tooltip("Maximum bank angle in degrees.")]
    public float maxBankAngle = 8f;
    [Tooltip("Minimum turn rate before any banking is applied (deg/m). Straights stay flat.")]
    public float bankTurnRateDeadzone = 0.08f;
    [Tooltip("How quickly bank target follows curvature changes (0..1 per sample).")]
    [Range(0.01f, 1f)] public float bankTargetResponse = 0.08f;
    [Tooltip("How fast bank angle can change (deg/m).")]
    public float bankChangeRateDegPerMeter = 1.0f;

    [Header("Railings")]
    [Tooltip("If true, rails are generated only for samples that come from designed road pieces.")]
    public bool railsOnlyOnDesignedPieces = true;
    [Tooltip("Minimum absolute local curvature (1/m) required to place railing.")]
    public float minCurvatureForRail = 0.01f;
    [Tooltip("Minimum merged railing span length in meters.")]
    public float minRailSpanLengthMeters = 6f;
    [Tooltip("Lateral offset from asphalt edge in meters.")]
    public float railLateralOffsetMeters = 0.25f;
    [Tooltip("Vertical offset above road surface in meters.")]
    public float railHeightMeters = 0.6f;
    [Tooltip("How far from each span edge beam cross-section tapers to zero (meters).")]
    public float railEndTaperMeters = 1f;
    [Tooltip("Distance from each rail span end over which the beam drops toward ground (meters).")]
    public float railEndDropDistanceMeters = 2.5f;
    [Tooltip("How far below ground the beam center ends at span tips (meters).")]
    public float railEndGroundClipDepthMeters = 0.08f;
    [Tooltip("Sampling step in meters used for railing extrusion.")]
    public float railSampleSpacingMeters = 1f;
    [Tooltip("Sideways-rotated W-beam depth from post face toward road center (meters).")]
    public float railBeamDepthMeters = 0.24f;
    [Tooltip("Beam cross-section height (meters).")]
    public float railBeamHeightMeters = 0.28f;
    [Tooltip("Beam material thickness for the sideways-W profile (meters).")]
    public float railBeamFlangeThicknessMeters = 0.055f;
    [Tooltip("Post spacing along the railing span (meters).")]
    public float railPostSpacingMeters = 2.5f;
    [Tooltip("Post width across road-normal axis (meters).")]
    public float railPostWidthMeters = 0.08f;
    [Tooltip("Post depth along road tangent axis (meters).")]
    public float railPostDepthMeters = 0.08f;
    [Tooltip("Post height (meters).")]
    public float railPostHeightMeters = 0.65f;
    [Tooltip("Post embed depth into ground (meters).")]
    public float railPostEmbedDepthMeters = 0.06f;

    [Header("Stations")]
    [Tooltip("Straight, flat road before a station's center (m), so arriving isn't sudden.")]
    public float stationApproachLength = 200f;
    [Tooltip("Straight, flat road before the start station's center (m). Short: the road bends just before it.")]
    public float startStationApproachLength = 45f;
    [Tooltip("Straight, flat road after a station's center (m).")]
    public float stationExitLength = 60f;
    [Tooltip("The road bends this much (degrees, random side) on the way to the start station, hiding where the police come from. The road starts aimed so it heads north after the bend.")]
    public float runInTurnDegrees = 90f;
    [Tooltip("Station lot beside the road (right side): x = depth away from the road, y = length along it at the back (m). Where it meets the road it is twice as long, tapering like a funnel.")]
    public Vector2 stationLotSize = new Vector2(20f, 26f);
    [Tooltip("Along the lot the right shoulder and ditch are flattened to road level; they blend back over this distance (m).")]
    public float stationApronBlend = 8f;
    [Tooltip("Terrain blends from lot height back to normal over this distance around the lot (m).")]
    public float stationLotTerrainBlend = 15f;
    [Tooltip("Where the road dead-ends, trees start this far past its end (m).")]
    public float roadEndTreeGap = 3f;

    [Header("Debug")]
    [Tooltip("Draw centerline and frame gizmos.")]
    public bool drawGizmos = true;
    [Tooltip("Draw frame vectors every N samples.")]
    public int gizmoFrameStride = 8;
    [Tooltip("Length of frame gizmos in meters.")]
    public float gizmoFrameLength = 1.5f;
    [Tooltip("Log numeric smoothness diagnostics when road is rebuilt.")]
    public bool logSmoothnessDiagnostics = true;
    [Tooltip("Warn when seam kink angle exceeds this value (degrees).")]
    public float seamKinkWarningDeg = 2.5f;
    [Tooltip("Draw gizmo markers at chunk seam boundaries.")]
    public bool drawSeamMarkers = true;
    [Tooltip("Seam marker sphere radius in meters.")]
    public float seamMarkerRadius = 0.6f;
    [Tooltip("Seam marker vertical line height in meters.")]
    public float seamMarkerHeight = 2.2f;
}
