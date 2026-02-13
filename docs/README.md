# Ralli — Design Documentation

## Game Design Specs

| Doc | Topic |
|-----|-------|
| [00 — Global Specs](00-global-specs.md) | Units, input system, configurability |
| [01 — Core Loop](01-core-loop.md) | Endless run, permadeath, style points |
| [02 — Player Pressure](02-player-pressure.md) | Time buffer, "the chase" threat system |
| [03 — Scoring](03-scoring.md) | Style points: drift, cornering, near-miss, multiplier |
| [04 — World Setting](04-world-setting.md) | Nordic forest, season/time/weather variation |
| [05 — Vehicle Physics](05-vehicle-physics.md) | RWD-first, raycast wheels, slip-based tires |
| [06 — Surfaces](06-surfaces.md) | Vertex color encoding, friction pipeline |
| [07 — Road Generation](07-road-generation.md) | Seeded procedural chunks, extrusion, banking |
| [08 — Traffic](08-traffic.md) | Two-lane obstacles, spawn density scaling |
| [09 — Service Stops](09-service-stops.md) | Post-MVP: roguelike upgrades, repairs, intel |
| [10 — Implementation Plan](10-implementation-plan.md) | Engineering phases A–D |
| [11 — Configuration](11-configuration.md) | ScriptableObject asset definitions |
| [12 — Notes / Non-Goals](12-notes-non-goals.md) | MVP scope boundaries |

## Implementation Plans

Plans live in [`plans/`](plans/) and are created per feature or phase.

## Dev Log

- Format spec: [`devlog/FORMAT.md`](devlog/FORMAT.md)
- Entries: [`devlog/entries/`](devlog/entries/)
