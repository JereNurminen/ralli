# Project Initialization Design

**Date:** 2026-02-13

## Goal

Initialize the Ralli Unity project as a Claude Code project with proper conventions, folder structure, and documentation. Reorder the implementation plan to prioritize vehicle physics over road generation.

## Decisions

- **C# style:** Standard Unity/.NET (PascalCase methods, camelCase private fields, `_` prefix). No namespaces.
- **Folder structure:** Hybrid — scripts by system (`Assets/Scripts/Vehicle/`), assets by type (`Assets/Materials/`).
- **Phase order:** Vehicle physics first (must feel good on a flat plane before roads exist).
- **Unity MCP:** Available for scene setup, script management, and testing.

## Deliverables

### 1. CLAUDE.md
Project root file with coding conventions, architecture decisions, folder structure, and development workflow notes.

### 2. .gitignore
Proper Unity .gitignore covering Library/, Temp/, Logs/, obj/, builds, IDE files.

### 3. Asset Folder Skeleton
```
Assets/
  Scripts/Vehicle/
  Scripts/Road/
  Scripts/Surfaces/
  Scripts/Gameplay/
  Scripts/Core/
  Materials/
  Prefabs/
  ScriptableObjects/
  Scenes/
  Settings/ (existing)
```

### 4. Remove Boilerplate
Delete TutorialInfo/ folder and Readme.asset.

### 5. Documentation Edits
- `docs/README.md` — index of all spec docs
- `docs/10-implementation-plan.md` — reorder: Phase A = Vehicle Physics, Phase B = Surfaces + Roads
- `docs/11-configuration-surfaces.md` — reorder configs, vehicle first
- Light tightening on other docs

## Next Step
Invoke writing-plans skill to create Phase A implementation plan.
