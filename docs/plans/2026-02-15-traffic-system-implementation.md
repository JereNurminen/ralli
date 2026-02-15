# Traffic System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add MVP two-lane procedural traffic as moving obstacles on streamed road chunks, with density scaling and stable spawning/despawning.

**Architecture:** Build traffic from the existing `RoadStreamGenerator` centerline samples/chunk layouts. Spawn lightweight AI car instances per chunk in lane-aligned poses, advance them at constant speed (with optional gentle avoid nudge), and despawn with chunk culling. Keep behavior deterministic from seed + chunk index and expose tuning via ScriptableObject config.

**Tech Stack:** Unity 6, C#, existing `RoadStreamGenerator`, ScriptableObject config, MonoBehaviour-based runtime systems, Unity Test Framework (EditMode).

---

### Task 1: Define Traffic Configuration

**Files:**
- Create: `Ralli/Assets/Scripts/Traffic/TrafficConfig.cs`
- Create: `Ralli/Assets/ScriptableObjects/Traffic/Traffic_Default.asset`
- Modify: `docs/11-configuration.md`

**Step 1: Write the failing test**

```csharp
[Test]
public void TrafficConfig_Defaults_AreValid()
{
    var cfg = ScriptableObject.CreateInstance<TrafficConfig>();
    Assert.Greater(cfg.minSpawnSpeedKph, 0f);
    Assert.Greater(cfg.maxSpawnSpeedKph, cfg.minSpawnSpeedKph);
    Assert.Greater(cfg.spawnDensityPerKmAtStart, 0f);
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` EditMode targeting `TrafficConfigTests`  
Expected: FAIL because `TrafficConfig` does not exist.

**Step 3: Write minimal implementation**

Create `TrafficConfig` with:
- speed range (km/h)
- lane offset from centerline (m)
- spawn density start/max and scaling factor
- safe spacing front/back (m)
- optional avoid nudge amplitude/frequency
- prefab list(s)

Create default asset at `Ralli/Assets/ScriptableObjects/Traffic/Traffic_Default.asset`.

**Step 4: Run test to verify it passes**

Run: `run_tests` EditMode targeting `TrafficConfigTests`  
Expected: PASS.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scripts/Traffic/TrafficConfig.cs Ralli/Assets/ScriptableObjects/Traffic/Traffic_Default.asset docs/11-configuration.md
git commit -m "Add traffic configuration asset and defaults"
```

### Task 2: Expose Road Sampling API for Traffic

**Files:**
- Modify: `Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs`
- Test: `Ralli/Assets/Tests/EditMode/Road/RoadStreamTrafficApiTests.cs`

**Step 1: Write the failing test**

```csharp
[Test]
public void TryGetPoseAtS_ReturnsFrameOnGeneratedRoad()
{
    var go = new GameObject("Road");
    var road = go.AddComponent<RoadStreamGenerator>();
    road.RebuildFromScratch();
    bool ok = road.TryGetPoseAtS(50f, out var pos, out var fwd, out var right, out var up);
    Assert.IsTrue(ok);
    Assert.Greater(fwd.magnitude, 0.9f);
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` EditMode targeting `RoadStreamTrafficApiTests`  
Expected: FAIL because API is missing.

**Step 3: Write minimal implementation**

Add read-only methods:
- `TryGetPoseAtS(float s, out Vector3 position, out Vector3 forward, out Vector3 right, out Vector3 up)`
- `GetChunkStartEndS(int chunkIndex, out float startS, out float endS)`

Implementation should reuse existing sample interpolation logic and chunk layout cache.

**Step 4: Run test to verify it passes**

Run: `run_tests` EditMode targeting `RoadStreamTrafficApiTests`  
Expected: PASS.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs Ralli/Assets/Tests/EditMode/Road/RoadStreamTrafficApiTests.cs
git commit -m "Expose road sampling API for traffic placement"
```

### Task 3: Build Traffic Actor Runtime

**Files:**
- Create: `Ralli/Assets/Scripts/Traffic/TrafficVehicle.cs`
- Test: `Ralli/Assets/Tests/EditMode/Traffic/TrafficVehicleTests.cs`

**Step 1: Write the failing test**

```csharp
[Test]
public void Tick_AdvancesAlongRoadDistance()
{
    var actor = new GameObject("TrafficVehicle").AddComponent<TrafficVehicle>();
    actor.Initialize(100f, 20f);
    actor.Tick(0.5f);
    Assert.Greater(actor.CurrentS, 100f);
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` EditMode targeting `TrafficVehicleTests`  
Expected: FAIL because `TrafficVehicle` is missing.

**Step 3: Write minimal implementation**

Implement:
- state: `CurrentS`, `SpeedMps`, lane side
- `Initialize(startS, speedMps, laneSign)`
- `Tick(dt)` advancing `CurrentS`
- `ApplyRoadPose(...)` for transform update
- optional sinusoidal lateral avoid nudge

**Step 4: Run test to verify it passes**

Run: `run_tests` EditMode targeting `TrafficVehicleTests`  
Expected: PASS.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scripts/Traffic/TrafficVehicle.cs Ralli/Assets/Tests/EditMode/Traffic/TrafficVehicleTests.cs
git commit -m "Add traffic vehicle runtime actor"
```

### Task 4: Implement Chunk-Based Traffic Spawner

**Files:**
- Create: `Ralli/Assets/Scripts/Traffic/TrafficStreamManager.cs`
- Modify: `Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs`
- Test: `Ralli/Assets/Tests/EditMode/Traffic/TrafficSpawnerTests.cs`

**Step 1: Write the failing test**

```csharp
[Test]
public void SpawnForChunk_UsesLaneOffsetsAndSpacing()
{
    // Arrange fake chunk range and deterministic seed
    // Assert no two traffic vehicles in same lane violate min spacing
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` EditMode targeting `TrafficSpawnerTests`  
Expected: FAIL because manager/spawn logic is missing.

**Step 3: Write minimal implementation**

Manager responsibilities:
- subscribe/poll active chunk range from `RoadStreamGenerator`
- deterministically compute per-chunk spawn candidates from seed + chunk index
- spawn 0..N vehicles per chunk based on density curve
- position in right/left lane: `laneOffsetFromCenter` using road right vector
- maintain dictionaries by `chunkIndex` and cull with chunk despawn

**Step 4: Run test to verify it passes**

Run: `run_tests` EditMode targeting `TrafficSpawnerTests`  
Expected: PASS.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scripts/Traffic/TrafficStreamManager.cs Ralli/Assets/Scripts/Road/RoadStreamGenerator.cs Ralli/Assets/Tests/EditMode/Traffic/TrafficSpawnerTests.cs
git commit -m "Add chunk-based procedural traffic spawner"
```

### Task 5: Hook Collision + Run-End Signal

**Files:**
- Create: `Ralli/Assets/Scripts/Traffic/TrafficCollisionProxy.cs`
- Modify: `Ralli/Assets/Scripts/Vehicle/CarController.cs`
- Modify: `Ralli/Assets/Scripts/Core/Debug/DebugOverlay.cs` (optional event display)
- Test: `Ralli/Assets/Tests/EditMode/Traffic/TrafficCollisionTests.cs`

**Step 1: Write the failing test**

```csharp
[Test]
public void CollisionWithTraffic_RaisesCrashEvent()
{
    // Create car + traffic collider, simulate contact callback path
    // Assert crash event fired once
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` EditMode targeting `TrafficCollisionTests`  
Expected: FAIL due to missing collision handling.

**Step 3: Write minimal implementation**

Add:
- `TrafficCollisionProxy` tag/component for traffic vehicles
- car-side event `OnTrafficCollision` (or game-state event) on collision enter
- keep MVP behavior simple: report crash, no advanced damage model

**Step 4: Run test to verify it passes**

Run: `run_tests` EditMode targeting `TrafficCollisionTests`  
Expected: PASS.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scripts/Traffic/TrafficCollisionProxy.cs Ralli/Assets/Scripts/Vehicle/CarController.cs Ralli/Assets/Tests/EditMode/Traffic/TrafficCollisionTests.cs
git commit -m "Add traffic collision signaling"
```

### Task 6: Scene Wiring + Debug Telemetry

**Files:**
- Create: `Ralli/Assets/Scripts/Traffic/Debug/TrafficDebugInfoProvider.cs`
- Modify: `Ralli/Assets/Scenes/SampleScene.unity`
- Modify: `Ralli/Assets/Scripts/Core/Debug/IDebugInfoProvider.cs` (if needed)

**Step 1: Write the failing test**

```csharp
[Test]
public void TrafficDebugProvider_ReportsActiveCounts()
{
    var provider = new GameObject("TrafficDebug").AddComponent<TrafficDebugInfoProvider>();
    var builder = new DebugPanelBuilder();
    provider.BuildDebugInfo(builder);
    StringAssert.Contains("Active Traffic", builder.ToString());
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` EditMode targeting `TrafficDebugInfoProviderTests`  
Expected: FAIL because provider is missing.

**Step 3: Write minimal implementation**

Expose:
- active traffic count
- spawned per chunk
- nearest traffic distance ahead
- density multiplier

Wire manager + config + prefabs in `SampleScene`.

**Step 4: Run test to verify it passes**

Run: `run_tests` EditMode targeting `TrafficDebugInfoProviderTests`  
Expected: PASS.

**Step 5: Commit**

```bash
git add Ralli/Assets/Scripts/Traffic/Debug/TrafficDebugInfoProvider.cs Ralli/Assets/Scenes/SampleScene.unity
git commit -m "Wire traffic system and add debug telemetry"
```

### Task 7: Validation Sweep

**Files:**
- Modify: `docs/devlog/entries/2026-02-15-<hash>-traffic-mvp.md`
- Modify: `docs/08-traffic.md` (if behavior differs from original bullets)

**Step 1: Run full relevant verification**

Run:
- `read_console` after refresh/compile
- `run_tests` EditMode for all `Traffic*` and `Road*Traffic*`
- manual play check in `SampleScene`

Expected:
- no compile errors
- tests pass
- vehicles spawn in both lanes and move forward
- despawn behavior stable with chunk culling

**Step 2: Record outcomes**

Document:
- final parameter values
- known limitations (no overtaking, simple avoid only)
- follow-up tasks (near-miss scoring integration)

**Step 3: Commit**

```bash
git add docs/08-traffic.md docs/devlog/entries/2026-02-15-*.md
git commit -m "Document MVP traffic system behavior and validation"
```

