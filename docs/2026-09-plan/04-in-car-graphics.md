# Phase 4 — In-car graphics

Depends on: Phase 3 (camera shake, gauges, and hand states read its outputs).
Style reference: Colin McRae Rally 2.0 era. In-car view only. Tone: anxious, stressed. Matte, low-gloss, simple shading; avoid modern PBR glossiness.

## Current state (verified)

- `Core/FollowCamera.cs`: chase cam, FOV `nearFieldOfView = 60` → `farFieldOfView = 76` + `boostFovAdd = 6`, `fovLerp = 6`.
- `Core/BoostPostProcessing.cs`: URP volume; `maxMotionBlurIntensity = 0.35`, `maxChromaticAberration = 0.12`, keyed to `BoostFactor`.
- No cockpit model, IK, or animation exists. Wheels are built from primitives.

## 4.1 Cockpit

- [ ] Acquire/model a low-poly retro hot-hatch interior: dashboard, A-pillars, steering wheel, gauges, gear/handbrake levers. Budget ~1.5–4k triangles for the visible shell; model only what the driver's view frustum sees.
- [ ] Detail via small hand-painted/baked textures (gauge faces, switches), not geometry. Keep steering wheel, gauge needles, and levers as separate objects for rigging.
- [ ] Candidate sources (search by name; no URLs recorded): itch.io "Low Poly Textured WRC Car with interior" (~$5, WIP quality); Sketchfab "Kiri '86" (CC BY, JDM compact, textured interior, usable as a base to reskin); custom low-poly commission.
- [ ] Steering wheel rotates with `Steer`; cap visual rotation (~±180° per lock) so hand IK never needs regrip.
- [ ] Speedometer needle ← `SpeedMps`. Heat gauge needle ← `Heat01`.
- [ ] **[STRETCH]** Tachometer ← `FakeRpm01`.

## 4.2 Camera

- [ ] Replace `FollowCamera` with a rigid chassis-anchored in-car camera (no positional/rotational lag).
- [ ] Shake driven by suspension/ground state and surface roughness; sharp jolts on impacts.
- [ ] Narrow, steady FOV; no speed/boost FOV ramp.
- [ ] Subtle head sway from lateral and longitudinal G.

## 4.3 Arms and hands

- [ ] Two-bone IK per arm (Animation Rigging `TwoBoneIKConstraint`), driven by procedural target transforms; elbow pole target per arm.
- [ ] Left hand: fixed target parented to the wheel rim.
- [ ] Right hand states: `Wheel`, `Handbrake` (driven by `Handbrake` bool). Blend target transitions over ~0.15–0.3 s. Keep targets within natural reach.
- [ ] **[STRETCH]** `Shifting` state: brief reach/grab/return pulse triggered by `GearIndex` changes (cosmetic only).
- [ ] Static grip poses per context (blend shape or mesh swap); no per-finger animation.
- [ ] Add small positional jitter to hand targets from the same signal driving camera shake.

## 4.4 Screen effects

- [ ] Extend `BoostPostProcessing` to pulse chromatic aberration/motion blur on hard impacts and near-misses, in addition to Overdrive.
- [ ] Light constant vignette.
- [ ] Windshield grime/dust/rain-streak screen-space overlay.

## 4.5 Mirrors

Decision: rearview mirror and left-side mirror work; no right-side mirror (intentional blind spot).

- [ ] Two extra cameras rendering to low-resolution `RenderTexture`s, shown on mirror planes in the cockpit model.
- [ ] Reduce update rate (e.g. every 2nd–3rd frame). They render the same scene, so no extra content is needed.
