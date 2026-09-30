# Phase 6 — Audio

Depends on: Phase 3 outputs (`SpeedMps`, `Heat01`, per-axle grip usage, impacts, surface type). Interior mix assumes Phase 4 camera. Asset sources: `06b-audio-asset-sources.md`.
No audio code or assets exist yet.

## Scope

In scope: engine, tires, surface, heat cues, impacts, mixer. Out of scope: ambience, music (added separately later). Pacenotes are **[STRETCH]**.

## Design rules

- The player's eyes are on the road: audio carries risk information (grip limit, engine heat, off-road), not just atmosphere.
- Use interior recordings for the engine (in-car view only).
- Digital inputs: audio must respond to time-held state, not analog magnitude.

## Tasks

### 6.1 Engine

- [ ] 3–4 seamless interior loops (idle/low/mid/high) crossfaded by speed and throttle state; limit pitch shift per clip (avoid stretching beyond ~500 RPM-equivalent from the recorded point).
- [ ] Overdrive: add a strained layer/pitch lift. Heat: add a roughness layer (stutter, misfire, muffler crackle) scaling continuously with `Heat01`, escalating near the taper threshold.
- [ ] Do not build a real RPM model; drive from speed, throttle, Overdrive, heat.
- [ ] **[STRETCH]** Gear-change sounds triggered by cosmetic `GearIndex` changes.

### 6.2 Tires

- [ ] Two looping squeal sources, positioned at front and rear axles. Volume and pitch scale continuously with per-axle grip usage (no binary threshold). Front-dominant and rear-dominant slides must be audibly distinct.
- [ ] Handbrake slides use the same rear-axle source.

### 6.3 Surface

- [ ] Rumble layers per surface (asphalt, dirt/ditch, gravel/off-road), crossfaded by the active surface type from Phase 3, volume scaled with speed. Serves as a legibility cue for leaving the road under tunnel vision.
- [ ] Define surface detection (raycast hit collider tag or mesh vertex color) if Phase 3 did not.

### 6.4 Impacts

- [ ] Graded by impact magnitude (tree, ditch wall, heavy hit); pool of variations per grade to avoid repetition.
- [ ] Trigger together with the Phase 4.4 screen pulse.

### 6.5 Mixing and tech

- [ ] `AudioMixer` groups: Engine, Tires, Surface, Impacts, (Pacenotes). Ducking rules per group.
- [ ] Pool `AudioSource`s; use `PlayOneShot` for one-shots.
- [ ] Randomize pitch/start offset on loops; keep variation pools for frequent one-shots.
- [ ] Listener fixed at the cockpit; low spatial blend for cabin sounds.

### 6.6 [STRETCH] Pacenotes

- [ ] Source data: upcoming curve direction/severity from the road generator (`GetTurnRateDegPerMeter(s)` look-ahead; `totalYawDeltaDeg` for designed pieces).
- [ ] Look-ahead distance scales with speed; dedupe repeated calls.
- [ ] Two presentations: visual (HUD icon/text, rally-style) and spoken (co-driver voice). Call vocabulary and voice source undecided.
