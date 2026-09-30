# Phase 1 — Terrain generation (structure only)

Scope: what geometry exists and where. Appearance (fog, canopy look, biomes, shading) is Phase 5.

## Current state (verified)

- `RoadStreamGenerator` builds each chunk mesh by extruding a lateral cross-section `BuildProfile(halfWidth)` along spline samples. `ProfilePoint { lateral, drop, color }`. Profile: skirt bottom → forest edge → ditch → shoulder → asphalt, mirrored. `dropSkirtDepth = 3`.
- Each point is offset by `sample.right * AdjustLateralForCurvature(point.lateral, sample.turnRateDegPerMeter)` (~L325, ~L439; function ~L854), a per-sample compression controlled by `innerProfileCompressionRadiusFactor = 0.8`.
- Elevation: `GetTargetElevation(s)` (~L1434), a 1D function of arc length. Config: `enableHills`, `largeHillAmplitude = 8`, `largeHillWavelength = 360`, `maxSlopeAngleDeg = 8`, `slopeResponse = 0.12`. Banking: `bankFromCurvature`, `maxBankAngle` (~L1340).
- Procedural curves: `minCurveTurnRateDegPerMeter = 0.12` … `maxCurveTurnRateDegPerMeter = 0.30` deg/m (min radius ≈ 191 m at max rate; R = 180 / (π · rate)).
- Curve direction (~L1547): first curve `Hash01(pieceIndex,3) < 0.5`; later curves flip vs previous with probability `oppositeCurveChance = 0.65` (favors alternation). No use of heading error.
- `SelectDesignedPiece` (~L1571) weights candidates by `headingError = DeltaAngle(targetBearing, cumulativeYawDeg)` and `headingCorrectionStrength = 0.4`. Procedural curves ignore both.
- Trees: `SpawnTreesForChunk` (~L899) samples triangles from `BuildForestTriangles(visualMesh)` (~L968). Colliders use `treeColliderHeight = 8` (~L1131).

## Root cause of the skirt overlap

Naive parallel offset: a point at lateral distance `d` on a curve of radius `R` traces radius `R − d`; when `d → R` the strip folds. `AdjustLateralForCurvature` patches this per sample, without cross-sample smoothing, without ring-order guarantees, and only for procedural curves. **Verify:** whether designed pieces are curvature-clamped anywhere (none found in the first 60 lines of `DesignedRoadPieceAuthoring.cs`).

## Target architecture

- Spline-coupled geometry is limited to a narrow corridor: road, shoulder, ditch, cut/fill face.
- All terrain beyond the corridor is a world-space heightfield `H(x,z)` (a height function cannot self-intersect for non-overhanging shapes).
- The road keeps its own grade-limited elevation; terrain accommodates it via cut-and-fill.
- Far-field visuals are handled in Phase 5 (no far geometry generated here).

## 1.1 Narrow the spline-extruded profile

- [ ] Reduce `BuildProfile` output to road + shoulder + ditch (+ cut/fill face from 1.3). Remove forest-floor and skirt points from the extruded profile.
- [ ] Keep `AdjustLateralForCurvature` only if the narrowed profile still needs it; otherwise delete it and `innerProfileCompressionRadiusFactor`.
- [ ] Define a curvature ceiling from the maximum lateral offset of the spline-coupled geometry (min radius must exceed max offset with margin). Enforce it for procedural curves (`maxCurveTurnRateDegPerMeter`) and for designed pieces at bake time in `DesignedRoadPieceAuthoringEditor`.
- [ ] Add a debug assert/gizmo that flags any chunk where adjacent profile rings invert order.

## 1.2 Heightfield

- [ ] Implement `H(x,z)`: layered noise (Perlin/simplex), seed-deterministic.
- [ ] Mesh on a fixed world-space grid (not spline-relative). Stream tiles in a radius around the camera, near/mid range only, independent of road-chunk streaming. Bias the radius toward `targetBearing` direction (see 1.5).
- [ ] Coarse resolution; tile size, radius, and noise params live in `RoadGenerationConfig`.
- [ ] Raise `largeHillAmplitude`-style relief through the heightfield rather than the road elevation function.

## 1.3 Cut-and-fill

Road grade stays gentle regardless of `H`; terrain adapts.

- [ ] Road-bed elevation follows the grade-limited path (`maxSlopeAngleDeg`, `slopeResponse`), independent of `H`.
- [ ] Where `H` > road elevation: **cut** face rising from the ditch edge to meet `H`. Where `H` < road elevation: **fill** embankment sloping down to meet `H`.
- [ ] Use a fixed, configurable face slope so lateral extent = depth / tan(slope).
- [ ] Cap cut/fill depth so lateral extent stays within the curvature-safe offset from 1.1. Keep the steepest/tallest `H` features away from the road.
- [ ] Banking (`bankFromCurvature`, `maxBankAngle`) is unchanged; it does not interact with cut-and-fill.
- [ ] Optional: procedural router prefers contour-following routes (reduces cut/fill), strictly as a tiebreaker beneath 1.5.

## 1.4 Pre-authored elevation

- [ ] Run the 1.3 cut-and-fill against a designed piece's authored elevation so procedural and designed stretches share one mechanism and elevation stays continuous at seams.
- [ ] Expose per-piece cut/fill amount to the piece author (minimal vs dramatic).
- [ ] Prefer procedural placement in locally gentle `H` regions where practical.

## 1.5 Directional bias

- [ ] In the procedural curve branch (~L1547), replace the direction choice with one weighted by `headingError` and `headingCorrectionStrength`. Correcting direction is `-sign(headingError)` given `headingError = cumulativeYawDeg − targetBearing` (wrapped); **verify** sign against `ScoreCandidate`.
- [ ] Keep `oppositeCurveChance` as the baseline alternation term; fold the heading term into the same probability.
- [ ] Raise `headingCorrectionStrength` for "heavily biased" stages (tunable).
- [ ] Apply the bearing bias to heightfield streaming (1.2).

## 1.6 Tree placement and collision

Decision: near-road trees have collision.

- [ ] Replace `BuildForestTriangles`-based scatter (mesh no longer exists in the old form) with world-space Poisson-disc or jittered-grid sampling. Reject points near the road via distance-to-spline. Base Y from `H` (or corridor height inside the corridor apron).
- [ ] Collider = vertical capsule/cylinder per tree, independent of visual representation. Reuse `treeColliderHeight` and the existing collider creation.
- [ ] Spawn/despawn colliders with the existing chunk streaming lifecycle to bound collision cost.
- [ ] Biome-specific prefab sets and density are Phase 5; build the mechanism parameterizable now.
