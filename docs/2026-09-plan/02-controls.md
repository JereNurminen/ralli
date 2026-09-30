# Phase 2 — Controls

Decision: no manual transmission. Two throttle inputs plus handbrake.

| Action | Input | Notes |
|--------|-------|-------|
| Normal throttle | `CarInputReader.Throttle` | Safe, understeer-prone. Unchanged. |
| Overdrive | `CarInputReader.Boost` | Repurposed; no new action. Straight-line speed boost, oversteer/drift in corners. |
| Handbrake | `CarInputReader.Handbrake` | Secondary role (see Phase 3). Unchanged. |
| Steer / Brake | `Steer`, `Brake` | Unchanged. Brake only slows the car. |

- [ ] Treat `Boost` as Overdrive throughout the code. Rename the property/field if it does not collide with `BoostFactor` / `BoostPostProcessing` usage; otherwise document the alias.
- [ ] Confirm no downshift input, clutch input, or auto-shift setting exists or is added.
