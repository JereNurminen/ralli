# Repository Guidelines

## Project Structure & Module Organization
- Unity project root is `Ralli/`.
- Game code lives in `Ralli/Assets/Scripts/` with domain folders:
  - `Core/` (camera, debug overlay, shared runtime glue)
  - `Vehicle/` (input, handling, controller, wheel visuals)
  - `Road/` (stream generation, designed road pieces, authoring)
  - `Road/Editor/` (editor-only tooling)
- Data assets are in `Ralli/Assets/ScriptableObjects/` and `Ralli/Assets/RoadPieces/`.
- Scenes are in `Ralli/Assets/Scenes/`.
- Design and planning docs are in `docs/` and `docs/plans/`.

## Build, Test, and Development Commands
- Open project in Unity 6 LTS (`6000.0.45f1`): open `Ralli/` in Unity Hub.
- Run EditMode tests headless:
  ```bash
  "$UNITY_EDITOR" -batchmode -nographics -projectPath Ralli -runTests -testPlatform EditMode -quit
  ```
- Run PlayMode tests headless:
  ```bash
  "$UNITY_EDITOR" -batchmode -nographics -projectPath Ralli -runTests -testPlatform PlayMode -quit
  ```
- In MCP-driven workflows, use `read_console` after script edits and `run_tests` before finishing work.

## Coding Style & Naming Conventions
- Follow Unity/.NET C# conventions used in this repo:
  - `PascalCase` for types, methods, and public members
  - `camelCase` for locals/parameters
  - private serialized/backing fields use `_camelCase`
- Keep Unity scripts in flat global scope (no namespaces unless project direction changes).
- Prefer small, focused MonoBehaviours and ScriptableObject-based configuration.
- Keep editor scripts under `Assets/Scripts/**/Editor/`.

## Testing Guidelines
- Unity Test Framework is installed (`com.unity.test-framework`).
- Add tests under `Ralli/Assets/Tests/EditMode/` or `Ralli/Assets/Tests/PlayMode/`.
- Test file naming: `FeatureNameTests.cs`; test methods should describe behavior (for example, `Steering_IsClamped_AtLowSpeed`).
- Run relevant test platform(s) for every gameplay/runtime change.

## Commit & Pull Request Guidelines
- Follow existing commit style: short, imperative subject lines (for example, `Improve road generation`, `Add designed road pieces system`).
- Keep commits scoped to one logical change.
- PRs should include:
  - concise summary and intent
  - linked issue/task (if available)
  - test evidence (console/test output)
  - screenshots or short clips for scene/visual changes
