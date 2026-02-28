# Railing Generation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add outside-only procedural railings on tight turns, driven by local curvature thresholds and configurable to run only on designed road pieces.

**Architecture:** Extend road generation config with railing settings, classify road samples by local signed curvature, convert classified samples into filtered side spans, and generate pooled rail meshes with colliders per chunk. Gate placement by sample source type (designed vs procedural) through a config toggle.

**Tech Stack:** Unity 6, C#, existing `RoadStreamGenerator` pipeline, ScriptableObject configuration, Unity Test Framework (EditMode).

---

### Task 1: Add Railing Configuration

**Files:**
- Modify: `Ralli/Assets/Scripts/Road/RoadGenerationConfig.cs`
- Modify: `docs/11-configuration.md`
- Test: `Ralli/Assets/Tests/EditMode/Road/RoadRailingConfigTests.cs`

**Step 1: Write the failing test**

```csharp
[Test]
public void RoadGenerationConfig_RailingDefaults_AreValid()
{
    var cfg = ScriptableObject.CreateInstance<RoadGenerationConfig>();
    Assert.IsTrue(cfg.railsOnlyOnDesignedPieces);
    Assert.Greater(cfg.minCurvatureForRail, 0f);
    Assert.Greater(cfg.minRailSpanLengthMeters, 0f);
    Assert.Greater(cfg.railSampleSpacingMeters, 0f);
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` EditMode targeting `RoadRailingConfigTests`  
Expected: FAIL because fields are missing.

**Step 3: Write minimal implementation**

Add fields to `RoadGenerationConfig`:
- `railsOnlyOnDesignedPieces`
- `minCurvatureForRail`
- `minRailSpanLengthMeters`
- `railLateralOffsetMeters`
- `railHeightMeters`
- `railEndTaperMeters`
- `railSampleSpacingMeters`

Document fields in `docs/11-configuration.md`.

**Step 4: Run test to verify it passes**

Run: `run_tests` EditMode targeting `RoadRailingConfigTests`  
Expected: PASS.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scripts/Road/RoadGenerationConfig.cs Ralli/Assets/Tests/EditMode/Road/RoadRailingConfigTests.cs docs/11-configuration.md
git commit -m "Add road railing configuration fields"
```

### Task 2: Add Curvature Classification API

**Files:**
- Modify: `Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs`
- Test: `Ralli/Assets/Tests/EditMode/Road/RailingPlacementTests.cs`

**Step 1: Write the failing test**

```csharp
[Test]
public void ClassifyRailSide_UsesOutsideOfLocalTurn()
{
    // Arrange generated sample data with known signed curvature
    // Assert positive curvature => right side, negative => left, low => none
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` EditMode targeting `RailingPlacementTests`  
Expected: FAIL because classification API is missing.

**Step 3: Write minimal implementation**

In `RoadStreamGenerator`, add:
- local signed curvature computation from neighboring tangents
- side classification (`None`, `Left`, `Right`) using threshold

Expose a narrow internal/read-only path used by tests (for example, helper method or test hook).

**Step 4: Run test to verify it passes**

Run: `run_tests` EditMode targeting `RailingPlacementTests`  
Expected: PASS.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs Ralli/Assets/Tests/EditMode/Road/RailingPlacementTests.cs
git commit -m "Add local curvature based rail side classification"
```

### Task 3: Build Rail Span Extraction + Filtering

**Files:**
- Modify: `Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs`
- Test: `Ralli/Assets/Tests/EditMode/Road/RailingPlacementTests.cs`

**Step 1: Write the failing test**

```csharp
[Test]
public void BuildRailSpans_MergesContiguousMarks_AndDropsShortSpans()
{
    // Arrange mark sequence with tiny noisy intervals
    // Assert contiguous sides merge, spans under min length are removed
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` EditMode targeting `RailingPlacementTests`  
Expected: FAIL because span extraction/filtering is missing.

**Step 3: Write minimal implementation**

Implement:
- rail mark-to-span conversion grouped by side
- minimum span length filter
- start/end taper metadata storage

Keep data structures minimal for MVP.

**Step 4: Run test to verify it passes**

Run: `run_tests` EditMode targeting `RailingPlacementTests`  
Expected: PASS.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs Ralli/Assets/Tests/EditMode/Road/RailingPlacementTests.cs
git commit -m "Add rail span extraction and minimum length filtering"
```

### Task 4: Add Designed-Piece Gating Toggle

**Files:**
- Modify: `Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs`
- Modify: `Ralli/Assets/Scripts/Road/DesignedRoadPiece.cs` (only if source tagging needs extension)
- Test: `Ralli/Assets/Tests/EditMode/Road/RailingPlacementTests.cs`

**Step 1: Write the failing test**

```csharp
[Test]
public void RailPlacement_RespectsDesignedPieceOnlyToggle()
{
    // Arrange mixed designed/procedural samples
    // Assert toggle true blocks procedural, toggle false allows both
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` EditMode targeting `RailingPlacementTests`  
Expected: FAIL because source gating is missing.

**Step 3: Write minimal implementation**

Ensure generated road samples carry source type (`Designed`/`Procedural`) in runtime data used by rail classification.
Apply gating before span extraction using `railsOnlyOnDesignedPieces`.

**Step 4: Run test to verify it passes**

Run: `run_tests` EditMode targeting `RailingPlacementTests`  
Expected: PASS.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs Ralli/Assets/Scripts/Road/DesignedRoadPiece.cs Ralli/Assets/Tests/EditMode/Road/RailingPlacementTests.cs
git commit -m "Gate rail placement to designed pieces via config toggle"
```

### Task 5: Generate Rail Mesh + Collider

**Files:**
- Create: `Ralli/Assets/Scripts/Road/RailingMeshBuilder.cs`
- Modify: `Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs`
- Test: `Ralli/Assets/Tests/EditMode/Road/RailingMeshTests.cs`

**Step 1: Write the failing test**

```csharp
[Test]
public void BuildRailMesh_ForValidSpan_CreatesRenderableMesh()
{
    // Arrange simple curved span with road frames
    // Assert vertex/triangle counts > 0
}

[Test]
public void BuildRailMesh_EndTaper_ReducesProfileWidthNearEnds()
{
    // Arrange taper distance > 0
    // Assert profile width near span edges is less than mid-span width
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` EditMode targeting `RailingMeshTests`  
Expected: FAIL because mesh builder is missing.

**Step 3: Write minimal implementation**

Create `RailingMeshBuilder` to:
- sample span at `railSampleSpacingMeters`
- place profile points at configured lateral/height offsets
- extrude a simple beam profile along the span
- apply taper scale over `railEndTaperMeters`

In chunk integration path, create/assign mesh and `MeshCollider`.

**Step 4: Run test to verify it passes**

Run: `run_tests` EditMode targeting `RailingMeshTests`  
Expected: PASS.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scripts/Road/RailingMeshBuilder.cs Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs Ralli/Assets/Tests/EditMode/Road/RailingMeshTests.cs
git commit -m "Add procedural railing mesh generation with collider"
```

### Task 6: Hook Chunk Lifecycle + Pooling

**Files:**
- Modify: `Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs`
- Modify: `Ralli/Assets/Scripts/Road/RoadChunkView.cs` (if rail render/collider objects are chunk-owned)
- Test: `Ralli/Assets/Tests/EditMode/Road/RailingChunkIntegrationTests.cs`

**Step 1: Write the failing test**

```csharp
[Test]
public void ChunkLifecycle_CreatesAndCullsRailingObjectsWithChunk()
{
    // Arrange generated chunk with railing span
    // Assert rail object exists while active and is removed/reused on cull
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` EditMode targeting `RailingChunkIntegrationTests`  
Expected: FAIL because lifecycle integration is missing.

**Step 3: Write minimal implementation**

Integrate rail build/update/release into chunk generation and culling paths.
Reuse existing pool patterns for chunk-owned objects to avoid allocations.
Ensure seam-safe handling at chunk boundaries (no duplicate overlap, no gaps).

**Step 4: Run test to verify it passes**

Run: `run_tests` EditMode targeting `RailingChunkIntegrationTests`  
Expected: PASS.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs Ralli/Assets/Scripts/Road/RoadChunkView.cs Ralli/Assets/Tests/EditMode/Road/RailingChunkIntegrationTests.cs
git commit -m "Integrate railing generation into road chunk lifecycle"
```

### Task 7: Scene Wiring + Final Verification

**Files:**
- Modify: `Ralli/Assets/Scenes/SampleScene.unity` (if config or material wiring needed)
- Modify: `docs/10-implementation-plan.md` (optional roadmap note)

**Step 1: Write/adjust final integration test if needed**

```csharp
[Test]
public void TightDesignedCurve_GeneratesOutsideRail_Only()
{
    // End-to-end assertion with designed piece data
}
```

**Step 2: Run targeted EditMode suites**

Run: `run_tests` EditMode targeting:
- `RoadRailingConfigTests`
- `RailingPlacementTests`
- `RailingMeshTests`
- `RailingChunkIntegrationTests`

Expected: PASS.

**Step 3: Run broader regression tests**

Run: `run_tests` EditMode for road generation related tests.  
Expected: PASS with no regressions.

**Step 4: Verify console**

Run: `read_console` after script edits.  
Expected: no new errors from railing integration.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scenes/SampleScene.unity docs/10-implementation-plan.md
git commit -m "Wire railing system and finalize road integration verification"
```

