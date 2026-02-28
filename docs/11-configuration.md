# 11. Configuration Surfaces (ScriptableObjects)

Recommended assets:
- `CarHandlingConfig` (steering, suspension, grip, tire slip curves, boost, power/brakes, stability aids)
- `SurfaceFrictionConfig` (μ values, wetness reduction, speed curve)
- `RoadProfile` (cross-section samples + band tags)
- `RoadGenerationConfig` (chunk length, sampling density, curvature/hills)
- `RoadGenerationConfig` railing settings:
  - `railsOnlyOnDesignedPieces`
  - `minCurvatureForRail`
  - `minRailSpanLengthMeters`
  - `railLateralOffsetMeters`
  - `railHeightMeters`
  - `railEndTaperMeters`
  - `railEndDropDistanceMeters`
  - `railEndGroundClipDepthMeters`
  - `railSampleSpacingMeters`
  - `railBeamDepthMeters`
  - `railBeamHeightMeters`
  - `railBeamFlangeThicknessMeters`
  - `railPostSpacingMeters`
  - `railPostWidthMeters`
  - `railPostDepthMeters`
  - `railPostHeightMeters`
  - `railPostEmbedDepthMeters`
- `DifficultyConfig` (threat speed curve, traffic density curve, score multipliers)
