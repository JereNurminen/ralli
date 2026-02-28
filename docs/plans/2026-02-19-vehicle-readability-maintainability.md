# Vehicle Readability and Maintainability Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Simplify the vehicle runtime code for readability and maintainability while preserving existing behavior and ScriptableObject-based inspector configuration.

**Architecture:** Keep `CarController` as the orchestrator MonoBehaviour and extract focused helper models for steering, drive-state, and wheel-force math. Preserve public API and serialized tuning paths, then validate with lightweight model tests plus Unity test run.

**Tech Stack:** Unity 6 LTS, C#, Unity Test Framework (`com.unity.test-framework`), MonoBehaviour + ScriptableObject configuration.

---

### Task 1: Baseline Verification and Safety Snapshot

**Files:**
- Modify: None
- Test: Existing project-wide tests via Unity runner (if discoverable)

**Step 1: Check working tree and target files**

Run: `git status --short` (from project root)
Expected: Current changes visible and understood before edits.

**Step 2: Capture current `CarController` size and method surface**

Run: `rg -n "^(\s)*(public|private|protected)" Ralli/Assets/Scripts/Vehicle/CarController.cs -n`
Expected: Method/property list captured for refactor parity checks.

**Step 3: Commit**

```bash
git add docs/plans/2026-02-19-vehicle-readability-maintainability-design.md docs/plans/2026-02-19-vehicle-readability-maintainability.md
git commit -m "Document vehicle readability refactor design and plan"
```

### Task 2: Extract Steering Model (TDD)

**Files:**
- Create: `Ralli/Assets/Scripts/Vehicle/CarSteeringModel.cs`
- Create: `Ralli/Assets/Tests/EditMode/Vehicle/CarSteeringModelTests.cs`
- Modify: `Ralli/Assets/Scripts/Vehicle/CarController.cs`

**Step 1: Write the failing test**

```csharp
[Test]
public void EvaluateSteerFactor_FadesAtConfiguredSpeed() { /* ... */ }

[Test]
public void ApplyFrontAirborneAuthority_ReducesSteeringWhenNoFrontWheelsGrounded() { /* ... */ }
```

**Step 2: Run test to verify it fails**

Run: Unity EditMode test run targeting `CarSteeringModelTests`.
Expected: FAIL due to missing model class/methods.

**Step 3: Write minimal implementation**

```csharp
public static class CarSteeringModel
{
    public static float EvaluateSteerFactor(float forwardSpeedMps, CarHandlingConfig handling) { /* ... */ }
    public static float ApplyFrontWheelAuthority(float steerAngleDeg, int groundedFrontWheels, CarHandlingConfig handling) { /* ... */ }
    public static float StepSteerAngle(float currentSteerAngleDeg, float steerInput, float steerFactor, CarHandlingConfig handling, float dt) { /* ... */ }
}
```

**Step 4: Integrate into `CarController`**

- Replace local steering calculations in `FixedUpdate`/`EvaluateSteerFactor` with model calls.
- Keep external property `CurrentSteerFactor` unchanged.

**Step 5: Run tests to verify pass**

Run: Unity EditMode tests for `CarSteeringModelTests`.
Expected: PASS.

**Step 6: Commit**

```bash
git add Ralli/Assets/Scripts/Vehicle/CarSteeringModel.cs Ralli/Assets/Scripts/Vehicle/CarController.cs Ralli/Assets/Tests/EditMode/Vehicle/CarSteeringModelTests.cs
git commit -m "Extract steering model from car controller"
```

### Task 3: Extract Drive State Model (TDD)

**Files:**
- Create: `Ralli/Assets/Scripts/Vehicle/CarDriveModel.cs`
- Create: `Ralli/Assets/Tests/EditMode/Vehicle/CarDriveModelTests.cs`
- Modify: `Ralli/Assets/Scripts/Vehicle/CarController.cs`

**Step 1: Write the failing tests**

```csharp
[Test]
public void UpdateReverseState_EngagesReverse_WhenBrakingNearStop() { /* ... */ }

[Test]
public void ComputeBoostFactor_RampsUpAndDownByConfiguredRates() { /* ... */ }

[Test]
public void ResolveFakeGear_UsesThresholdsOrEvenSpacing() { /* ... */ }
```

**Step 2: Run test to verify it fails**

Run: Unity EditMode test run targeting `CarDriveModelTests`.
Expected: FAIL due to missing class/methods.

**Step 3: Write minimal implementation**

```csharp
public static class CarDriveModel
{
    public static bool UpdateReverseState(bool inReverse, float brakeInput, float throttleInput, float signedForwardSpeedMps) { /* ... */ }
    public static float StepBoostFactor(float currentBoost, bool boostHeld, CarHandlingConfig handling, float dt) { /* ... */ }
    public static void ResolveFakeGear(/* params */, out int gear, out float rpm01) { /* ... */ }
}
```

**Step 4: Integrate into `CarController`**

- Route reverse/boost/fake-gear updates through model methods.
- Preserve serialized config semantics and current public output fields.

**Step 5: Run tests to verify pass**

Run: Unity EditMode tests for `CarDriveModelTests`.
Expected: PASS.

**Step 6: Commit**

```bash
git add Ralli/Assets/Scripts/Vehicle/CarDriveModel.cs Ralli/Assets/Scripts/Vehicle/CarController.cs Ralli/Assets/Tests/EditMode/Vehicle/CarDriveModelTests.cs
git commit -m "Extract drive state model from car controller"
```

### Task 4: Simplify Wheel Simulation Readability

**Files:**
- Create: `Ralli/Assets/Scripts/Vehicle/WheelForceModel.cs`
- Modify: `Ralli/Assets/Scripts/Vehicle/CarController.cs`

**Step 1: Write the failing test (optional if pure extraction remains equivalent)**

```csharp
[Test]
public void CalculateSlipIntensity_ClampedAndStable() { /* ... */ }
```

**Step 2: Run test to verify it fails (if created)**

Run: Unity EditMode targeted test.
Expected: FAIL before implementation.

**Step 3: Write minimal implementation**

```csharp
public static class WheelForceModel
{
    public static float ComputeLateralGrip(float lateralSpeed, float gripScale, AnimationCurve slipCurve) { /* ... */ }
    public static float ComputeLongitudinalGrip(float slipRatio, float slipScale, AnimationCurve slipCurve) { /* ... */ }
}
```

**Step 4: Refactor `CarController` wheel code for clarity**

- Add helper methods for repeated reset/telemetry assignments.
- Keep physics formulas and call order unchanged where feasible.
- Reduce inline branching depth with named intermediates.

**Step 5: Run tests to verify pass**

Run: targeted EditMode tests (if added) + project EditMode tests.
Expected: PASS.

**Step 6: Commit**

```bash
git add Ralli/Assets/Scripts/Vehicle/WheelForceModel.cs Ralli/Assets/Scripts/Vehicle/CarController.cs
git commit -m "Refactor wheel force calculations for readability"
```

### Task 5: Integration Verification

**Files:**
- Modify: None
- Test: runtime + Unity tests

**Step 1: Refresh/compile Unity project**

Run (MCP): `refresh_unity` with compilation.
Expected: No compiler errors.

**Step 2: Check Unity console**

Run (MCP): `read_console`.
Expected: No new errors from refactor.

**Step 3: Run relevant tests**

Run (MCP): `run_tests` for EditMode (and PlayMode if affected).
Expected: PASS or clear failure diagnostics.

**Step 4: Commit**

```bash
git add Ralli/Assets/Scripts/Vehicle Ralli/Assets/Tests/EditMode/Vehicle
git commit -m "Simplify vehicle code for readability and maintainability"
```
