# Designed Road Pieces

## Problem

The procedural road generation (`RoadStreamGenerator`) produces an infinite stream of random straights and curves. While statistically varied, the result lacks memorable moments — no hairpins, chicanes, S-curves, or dramatic sequences.

## Solution

Hand-authored road pieces that the RoadStream places among procedural filler at distance-based intervals, with heading-correction weighting to prevent the road from circling on itself.

## Architecture

### Data Model

- **`DesignedRoadPiece`** (ScriptableObject) — Baked piece data: turn rate curve, optional elevation curve, arc length, total yaw delta, entry/exit turn rates.
- **`DesignedRoadPiecePool`** (ScriptableObject) — Weighted collection of pieces with mirroring support.
- **`RoadGenerationConfig`** — Extended with pool reference, distance scheduling, and heading correction parameters.

### Authoring Pipeline

1. Open `Scenes/RoadPieceAuthoring.unity`
2. Draw a spline (centerline) using Unity Splines tools
3. `DesignedRoadPieceAuthoring` component shows preview mesh and turn rate gizmo
4. "Bake" button samples spline, computes curvature and elevation, writes to `DesignedRoadPiece` asset
5. Add baked piece to a `DesignedRoadPiecePool`, assign pool to `RoadGenerationConfig`

### Runtime Integration

- `AdvancePiece()` checks a distance-based threshold; when exceeded, selects a designed piece from the pool
- Selection weights candidates by heading correction score: pieces that steer toward `targetBearing` are preferred
- `GetTurnRateDegPerMeter()` evaluates the piece's `AnimationCurve` instead of a constant rate
- `GetTargetElevation()` uses the piece's elevation curve when present
- Mirroring negates turn rates, doubling the effective piece variety

### Key Files

| File | Role |
|------|------|
| `Scripts/Road/DesignedRoadPiece.cs` | Runtime data (ScriptableObject) |
| `Scripts/Road/DesignedRoadPiecePool.cs` | Weighted collection |
| `Scripts/Road/RoadGenerationConfig.cs` | Config fields for scheduling/selection |
| `Scripts/Road/RoadStreamGenerator.cs` | Runtime integration |
| `Scripts/Road/DesignedRoadPieceAuthoring.cs` | Spline sampling + bake logic |
| `Scripts/Road/Editor/DesignedRoadPieceAuthoringEditor.cs` | Custom inspector with bake button |
| `Scenes/RoadPieceAuthoring.unity` | Dedicated authoring scene |

### Dependencies

- Unity Splines package (`com.unity.splines` 2.8.3) — authoring only, no runtime dependency
