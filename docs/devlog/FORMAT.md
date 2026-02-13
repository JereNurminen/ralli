# Dev Log Entry Format (Spec)

## File Path Pattern
- `docs/devlog/entries/YYYY-MM-DD-<shortHash>-<slug>.md`

## Required Fields
- `entry_id`: string (`YYYY-MM-DD-<shortHash>-<slug>`)
- `timestamp_utc`: ISO-8601 UTC datetime (`YYYY-MM-DDTHH:MM:SSZ`)
- `commit`
  - `hash`: full 40-char SHA-1
  - `short`: short hash
  - `title`: commit subject
- `phase`: enum (`PhaseA|PhaseB|PhaseC|PhaseD|Other`)
- `scope`: list of subsystem ids
- `changes`: list of implemented technical deltas
- `parameters`: list of tuned constants / defaults
- `scene_changes`: list of scene object/component changes
- `validation`: list of checks/results
- `known_issues`: list (can be empty)
- `next`: list of queued technical tasks

## Optional Fields
- `breaking_changes`: list
- `migration`: list
- `links`: list of doc/file paths

## Value Rules
- No narrative paragraphs.
- Bullet lists only.
- Use metric units for values.
- Include file paths for each code-level change.
- Keep numbers explicit (no relative adjectives).
