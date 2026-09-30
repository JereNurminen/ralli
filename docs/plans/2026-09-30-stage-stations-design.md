# Stages Between Gas Stations — Design

Date: 2026-09-30
Status: Draft for review

## Goal

Split the endless drive into **stages**. A run starts with the car parked, engine off, at a gas
station. Each stage is a freshly generated road that ends at another gas station. Reaching that
station's lot ends the stage; the next stage is generated there, with the time of day moved on and
the police starting as far behind as the player's lead was. The run ends when the police catch the
player (later also when the car is wrecked).

The world should feel eerie: the road comes out of the woods behind the start station and dead-ends
in the woods past the finish station.

## Decisions (from the design conversation)

- The station sits **beside** the road on a flat straight, forecourt on the right.
- The player must **drive onto the station lot** to finish. The finish area is a trigger collider
  on the station prefab, sized by hand in the editor.
- On entering it, the car does an **assisted fast stop** and the stage is done. No parking skill.
- The road is a **dead end** about 150 m past the finish station. Overshooting means turning back.
- The police do not catch a finished player: they **blast past** on the straight, drive off the end
  of the road into the trees and disappear.
- Stations cannot be skipped. The stop is where the next stage is generated and the day cycles.
  (Later: route choice, biome changes, shop.)
- **Each stage is a fresh generation** (scene reload), not one continuous road.
- **Every stage starts heading north.** The road leaves `s = 0` pointing world north (+Z), so the
  start station always faces the same way.
- **One seed per run.** Each stage's road (curves and elevation) is derived from the run seed and
  the stage number, so a run seed always reproduces the same sequence of stage roads. Trees and
  traffic are not seeded and differ every time.

## Stage road layout

```
 woods |~~ run-in ~200 m ~~|== START station ==|~~~ stage road ~~~|== FINISH station ==|~~ ~150 m ~~| woods
       police appear here    car parked,                              drive onto lot =     road ends,
                             engine off                               fast stop, done      police vanish
```

All distances are along the road (`s`) and configurable:

| Mark | Position |
|------|----------|
| Road start | `s = 0` |
| Start station (center) | `s = runInLength` (default 200 m) |
| Finish station (center) | `s = runInLength + stageLength` (stageLength per stage) |
| Road end | finish station + `deadEndLength` (default 150 m) |

## Part 1: stations on the road

### Station stretch (road generator)

- A new piece type, **Station**, about 120 m long, centered on each station's `s`. Turn rate 0,
  slope 0, no small bumps, no banking. It is forced at those `s` ranges; procedural and designed
  pieces are placed around it. The existing turn-rate/bank easing makes the road straighten
  smoothly going in.
- Along the forecourt, the **right side's shoulder and ditch are replaced by a flat apron** at road
  level (blending back to the normal profile at each end), so the car drives straight from the lane
  onto the lot.
- The generator stops producing samples at the **road end**. The last chunk ends there; no railing
  or cap is needed.

### Terrain and trees

- `TerrainHeightField` gets **flat zones**: an oriented rectangle (station lot + margin) at a fixed
  height. Inside it the height is the pad height; around it the terrain blends back over a margin.
- No trees spawn inside a flat zone.
- At both road ends the terrain band closes around the end (the band-edge wall wraps it) and trees
  fill the space in front of the road end, so the road visibly runs into the woods.

### Station prefab

`Prefabs/GasStation.prefab`, built from the kyjhi PSX Gas Station Pack (`station-complete` model,
16.5 × 18.6 m, canopy + pumps on a concrete floor). Imported to
`Assets/ThirdParty/kyjhi_PSXGasStationPack/` and logged in `docs/third-party-assets.md`.

Prefab frame: origin at the right road edge at the station center, +Z along the road, lot toward +X.
Contents:

- The station model, with colliders on pumps/pillars.
- `FinishZone`: a `BoxCollider` (trigger) covering the lot, hand-tuned. Only enabled on the finish
  station.
- `StartSpot`: a transform where the car is placed on the start station. Placed by hand in the
  prefab (the prefab ships with a rough placeholder position).

### Arrival: fast stop

- When the player's car enters the finish station's `FinishZone`, the stage is **finished**.
- `CarController` gets an assisted stop: drive input is ignored and a strong deceleration
  (about 25 m/s², configurable) brings the car to rest. Steering input is ignored too.
- Scoring stops. Music fades out (`MusicDirector.FinishStage`).

### Police at the finish

- On finish the van leaves the chase/ram loop and switches to **pass-through**: left lane, full
  chase speed, its collider disabled (so it cannot hit the stopped player).
- It follows the road to the road end, then keeps going straight along the last road direction into
  the trees, fades out and is destroyed after about 60 m.
- If the van has not spawned yet, it never appears.
- The player cannot be caught after finishing.

### Lead

Measured at the moment of finishing, as time:

- Van chasing: `lead = gap to player / chase speed`.
- Van not spawned yet: `lead = remaining start delay + spawn distance / chase speed`.

The next stage's police start delay = `lead` (clamped to a minimum, default 3 s). After that delay
(which only runs once the engine is on) the van appears its usual spawn distance behind the player,
never before the road start, so at the very start it comes out of the woods behind the start
station.

## Part 2: stage flow

### RunState

A static class (survives scene reloads) holding:

- `StageIndex` (0-based)
- `RunSeed` (random at run start)
- `LeadSeconds` (from the last finish; unset on stage 0)
- `Score` (carried across stages)

`RunState.StartNewRun()` resets it. It is also reset when the game starts in the editor.

### Stage settings

`StageProgression` ScriptableObject: a list of `StageSettings`. Stage `i` uses entry
`min(i, count - 1)`. Each entry:

- `stageLength` (m)
- `lightingPreset` (`LightingPreset`, e.g. Dawn → Day → Dusk → Night → …)
- `roadWidth`
- `trafficDensity`
- `policeChaseSpeedKph`

Road material joins once the surface system exists. Settings are applied to **runtime copies** of
the config assets (`Instantiate`), never to the assets themselves.

**Seeding:** the road seed is `RunSeed + StageIndex * 7919` and drives only the road's curves,
designed-piece choice and elevation (including the terrain heightfield it follows). Tree placement
and traffic use unseeded randomness. The road's first sample always heads world north (+Z),
independent of the `RoadStream` object's rotation.

### StageDirector

A new scene component (`Scripts/Gameplay/StageDirector.cs`) that runs the stage:

1. **Scene start:** read `RunState` and `StageProgression`; apply the stage settings and seed to the
   road, lighting, traffic and police; set the police start delay from the lead (stage 0: the
   config's own delay). Once the road is ready, place the two station prefabs, enable the finish
   station's `FinishZone`, and put the car at the start station's `StartSpot`, engine off. From
   here the existing run start takes over: starting the engine unfreezes traffic, starts the police
   delay and the music.
2. **Finish** (car enters `FinishZone`): fast stop, stop scoring, fade music, police pass-through,
   record lead and score into `RunState`. About 3 s later fade to black and show a placeholder card
   ("Stage 2 complete · Lead 12.4 s · Score …"). This is where the shop will go. A key press
   (Enter / gamepad South) advances `StageIndex` and reloads the scene.
3. **Caught** (`PoliceChaser.CaughtPlayer`): fade to a "Caught" card with the final score. A key
   press starts a new run (`RunState.StartNewRun()`) and reloads the scene.

A scene reload per stage is deliberate: traffic, police, music, scoring and ignition already handle
a fresh scene correctly, so nothing needs a manual reset.

### Traffic

Traffic spawns and despawns only on the stage road between the two station stretches, so no car
drives out of a dead end or through a station.

## Components touched

| Area | Change |
|------|--------|
| `RoadStreamGenerator` | Station piece type, forced station stretches, right-side flat apron, road end, stage layout input |
| `TerrainHeightField` / `TerrainStreamer` | Flat zones, no trees in them, band closes around road ends |
| `CarController` | Assisted fast stop |
| `PoliceChaser` | Start delay and speed from stage, pass-through mode, lead query |
| `TrafficStreamManager` | Spawn range limited to the stage road |
| `ScoreSystem` | Start from / write back to `RunState`, stop on finish |
| `LightingDirector` | Apply a preset chosen at scene start |
| New | `RunState`, `StageProgression`, `StageSettings`, `StageDirector`, `StationZone` (finish trigger), `GasStation.prefab`, stage card (IMGUI placeholder) |

## Out of scope

Shop, damage, route choice, biome changes, dialog/exposition, pre-generation and light baking,
win condition.

## Verification

No unit tests (project preference). Compile check after each step, then playtest:

1. Run starts parked at the start station, engine off; road behind ends in the woods.
2. Terrain is flat under both lots; no trees on them; no ditch lip onto the lot.
3. Driving past the finish lot leads to a dead end ~150 m on; turning back and entering the lot
   finishes.
4. Entering the lot stops the car within about a second; the card shows stage, lead, score.
5. A close police van passes in the left lane, runs off the road end into the trees and vanishes.
6. Next stage: new road heading north, next lighting preset, score carried over, police appear
   after the lead.
7. Same run seed (set by hand) twice: identical stage roads and elevation; trees and traffic differ.
8. Being caught shows the Caught card; continuing starts over at stage 1.

## Revisions after the first playtest

- Station lots are paved in the road material and shaped like a funnel: twice the lot length where
  they meet the road, tapering to the lot length at the back. The lot counts as road corridor for
  terrain, so the tree strip and boundary wall continue behind it.
- Station stretches are asymmetric: 200 m of straight before the finish station, 45 m before the
  start station, 60 m after either.
- The run-in bends about 90° (random side) into the start station, hiding where the police come
  from. The road starts aimed so the stage still heads north after the bend.
- Every stage: traffic spawning and the police countdown (start delay, or the previous lead) begin
  only when the player drives off the start lot.
- Shoulders scale with the stage's road width.
