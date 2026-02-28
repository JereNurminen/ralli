# Railing Generation Design

**Date:** 2026-02-20

## Goal

Add procedural roadside railings for tight turns using local curvature, generated on the outside of the turn, with an initial scope focused on designed road pieces.

## Scope

- Rails are generated from road geometry at runtime.
- Placement uses local signed curvature (not segment total angle).
- Rails appear only on the outside edge of turns.
- Procedural filler segments are excluded by default via config.
- Rail mesh and collider are separate from the road terrain mesh.

## Configuration

Extend `RoadGenerationConfig` with:

- `railsOnlyOnDesignedPieces` (`bool`, default `true`)
- `minCurvatureForRail` (`float`, units `1/m`)
- `minRailSpanLengthMeters` (`float`)
- `railLateralOffsetMeters` (`float`)
- `railHeightMeters` (`float`)
- `railEndTaperMeters` (`float`)
- `railSampleSpacingMeters` (`float`)

## Placement Logic

1. Compute local signed curvature at each road sample from neighboring centerline tangents.
2. If `abs(curvature) < minCurvatureForRail`, mark sample as no rail.
3. If above threshold:
   - positive curvature => outside is right
   - negative curvature => outside is left
4. If `railsOnlyOnDesignedPieces` is enabled, suppress any samples from procedural filler segments.
5. Merge adjacent same-side marks into spans.
6. Drop spans shorter than `minRailSpanLengthMeters`.
7. Apply end taper distance at both ends for mesh termination.

## Geometry + Collision

- Generate a dedicated rail mesh per active span (or per side span group).
- Use road frame vectors to position samples:
  - `position = splinePosition + right * (halfRoadWidth + railLateralOffsetMeters) + up * railHeightMeters`
- Use a simple beam profile for MVP.
- Assign a dedicated rail material.
- Assign `MeshCollider` to rail mesh object.
- Keep posts out of MVP (can be added later as cosmetic instancing).

## Runtime Integration

- Integrate into road chunk generation lifecycle.
- Compute placement marks/spans from sampled road data already used for chunk mesh generation.
- Build and recycle rail objects with chunk pooling.
- Ensure seams do not duplicate or gap across chunk boundaries.

## Testing Strategy

EditMode tests:

- Curvature below threshold produces no rail spans.
- Positive/negative curvature maps to right/left outside side.
- Short noisy spans are filtered by minimum span length.
- `railsOnlyOnDesignedPieces=true` blocks procedural spans.
- `railsOnlyOnDesignedPieces=false` allows spans from both sources.
- Generated mesh has valid geometry and a collider when a span exists.
- End taper reduces profile width near span boundaries.

## Non-Goals (MVP)

- Difficulty-driven guardrail progression.
- Decorative post instancing or breakaway behavior.
- Advanced impact VFX/SFX integration.

