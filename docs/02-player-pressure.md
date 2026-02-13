# 2. Player Pressure: Time Buffer ("The Chase")

- Player has a **time buffer** ahead of an unseen threat.
- Buffer increases when the player gains time (speed + performance); decreases when slowing/crashing/choosing time-cost actions.
- Threat baseline speed is constant; buffer change depends on player speed relative to baseline.
- Soft cap: additional time gained beyond a window has diminishing returns (prevents permanent safety).

**Design intent:** runs can enter a "flow" state but must remain risky; avoid a permanent runaway.
