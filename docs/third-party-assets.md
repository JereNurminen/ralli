# Third-Party Assets

Everything from outside this project lives in `Ralli/Assets/ThirdParty/` (git-ignored).
Add a row here when an asset enters the project, and keep the row even if the asset is
removed, so the record of what was tried stays complete. Before shipping, every row with
status **In use** must have its credit/licence obligations met.

Back up `Ralli/Assets/ThirdParty/` (with its `.meta` files) outside git. The `.meta` files
hold the GUIDs that scenes and prefabs reference; re-importing without them breaks links.

| Asset | Author | Source (URL) | Licence | Credit required? (exact text) | Path in project | Status | Added |
|-------|--------|--------------|---------|-------------------------------|-----------------|--------|-------|
| WRAD ARMS (first-person arms: `arms.fbx`, pale + dark albedo) | wriks (dark skin texture: Royalty3D) | https://wriks.itch.io/wrad-arms | CC0 1.0 | Not required (CC0). Author requests: "'WRAD ARMS' by wriks: https://wriks.motorcycles" | `Assets/ThirdParty/WRAD_ARMS/` | Evaluating | 2026-09-29 |

**Status values:** Evaluating · In use · Removed

**Licence notes:** CC BY = credit required. CC0 = none required, but credit anyway. Asset Store
Standard EULA = no credit required, but can't be redistributed as source. itch.io = check the page.
