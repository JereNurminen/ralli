# Third-Party Assets

Everything from outside this project lives in `Ralli/Assets/ThirdParty/` (git-ignored).
Add a row here when an asset enters the project, and keep the row even if the asset is
removed, so the record of what was tried stays complete. Before shipping, every row with
status **In use** must have its credit/licence obligations met.

Back up `Ralli/Assets/ThirdParty/` (with its `.meta` files) outside git. The `.meta` files
hold the GUIDs that scenes and prefabs reference; re-importing without them breaks links.

| Asset | Author | Source (URL) | Licence | Credit required? (exact text) | Path in project | Status | Added |
|-------|--------|--------------|---------|-------------------------------|-----------------|--------|-------|
| Classic JDM Sports Car (Interior & Rigged) - Low Poly (in-pack name "Unbranded Urban Car V01", `UUC_V01`; includes `wooden_studio_15_4k.exr`, a Poly Haven HDRI) | Polyeler | https://assetstore.unity.com/packages/3d/vehicles/land/classic-jdm-sports-car-interior-rigged-low-poly-362018 | Standard Unity Asset Store EULA | Not required. Suggested: "Classic JDM Sports Car by Polyeler". Raw files must not be redistributed | `Assets/ThirdParty/Polyeler/UnbrandedUrbanCar_V1/` | Evaluating | 2026-09-29 |
| Brutal Skyboxes (47 equirectangular 4096×1024 skies: `skybox_emx_*`, `skybox_plain_emx_*`) | Pizza Doggy | https://pizzadoggy.itch.io/brutal-skyboxes | Pizza Doggy License Agreement (2026-09-01): non-exclusive, worldwide, perpetual; commercial + non-commercial games; may modify and use in marketing. **Restrictions section (§3+) not yet reviewed** | **Unknown: not in §1–2; check §3+ of the PDF** | `Assets/ThirdParty/PizzaDoggy_BrutalSkyboxes/` | Evaluating | 2026-09-29 |
| Textured LowPoly Trees (Birch, Pine, DeadBirch, DeadTree, Tree models + bark/leaf textures) | Quaternius | https://quaternius.itch.io/textured-lowpoly-trees | CC0 1.0 | Not required (CC0). Suggested: "Textured LowPoly Trees by Quaternius" | `Assets/ThirdParty/Quaternius_TexturedLowpolyTrees/` | In use (roadside trees; our `Materials/Trees/*.mat` use its textures) | 2026-02-15 |
| WRAD ARMS (first-person arms: `arms.fbx`, pale + dark albedo) | wriks (dark skin texture: Royalty3D) | https://wriks.itch.io/wrad-arms | CC0 1.0 | Not required (CC0). Author requests: "'WRAD ARMS' by wriks: https://wriks.motorcycles" | `Assets/ThirdParty/WRAD_ARMS/` | Evaluating | 2026-09-29 |

**Status values:** Evaluating · In use · Removed

**Licence notes:** CC BY = credit required. CC0 = none required, but credit anyway. Asset Store
Standard EULA = no credit required, but can't be redistributed as source. itch.io = check the page.
