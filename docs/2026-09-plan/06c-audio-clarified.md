# Adaptive Music Director – Prototype Implementation Plan

## Goal

Implement a lightweight music system for the Unity rally prototype using the purchased **Race Tracks** music.

Each stage is expected to last roughly **five minutes or less**, while each song provides approximately four minutes of material:

```text
Intro → Part A (~1:55) → Part B (~1:55)
```

Rather than dynamically selecting A/B based on gameplay, use this existing progression as the stage's musical arc. Gameplay affects the **mix**, not the song arrangement.

---

## 1. Stage Playback

At the beginning of a rally stage:

```text
Intro
  ↓
A
  ↓
B
  ↓
B
  ↓
...
```

- Play the intro once.
- Seamlessly schedule A after the intro.
- Seamlessly schedule B after A.
- If the stage continues beyond B, loop B until the stage finishes.
- Use Unity DSP scheduling (`AudioSettings.dspTime` / `AudioSource.PlayScheduled`) rather than frame-based timing for seamless transitions.

No gameplay logic is needed to decide between A and B.

### Stage Finish

When the player reaches a breather/upgrade stop:

- Smoothly fade out the stage music.
- Stop/reset the stage playback state.
- The stop can have separate ambience/music if desired.

### Continuing After a Stop

Depending on the desired run structure:

- Start a different song from its intro, or
- Continue using the same song but restart from **Part A**.

This gives each new driving section a lower-energy beginning without necessarily replaying the intro.

---

## 2. Dynamic Mix

The arrangement follows the fixed progression, but the director periodically checks gameplay state and adjusts the mix.

### Speed → Volume

Vehicle speed controls a small music-volume boost.

For example:

```text
Normal driving       +0 dB
High speed           +1–3 dB
```

Do not map speed directly to volume every frame.

The director periodically determines a target boost, while the actual mixer parameter smoothly interpolates toward that target.

### Skill / Combo → Bass

Good driving or an active skill combo increases bass emphasis.

Since the current assets don't provide stems, use an EQ on the Music AudioMixer group rather than changing the actual bass instrument.

Keep the effect relatively subtle.

For example:

```text
Normal               0 dB EQ boost
Good combo           +2–4 dB bass emphasis
```

The exact frequency, gain and criteria should be tuned by ear during gameplay.

### State Checking

Persistent gameplay state is **queried by the MusicDirector**, rather than pushed to it.

For example:

```csharp
void EvaluateMix()
{
    targetVolumeBoost =
        car.IsDrivingFast ? highSpeedBoost : 0f;

    targetBassBoost =
        score.HasGoodCombo ? comboBassBoost : 0f;
}
```

A coroutine can evaluate these conditions periodically, perhaps every `0.25–0.5s`.

`Update()` only needs to smoothly interpolate the current mixer values toward those targets.

---

## 3. Crash / Impact Ducking

Impacts are instantaneous events, so they are **pushed to the MusicDirector** rather than queried.

```csharp
musicDirector.OnImpact(severity);
```

Severity determines the strength of the response.

Example:

```text
Small collision
    → little or no music change

Significant crash
    → fast ~10 dB duck
    → quick recovery

Major crash
    → fast ~20 dB duck / near silence
    → optional brief silence
    → slower recovery
```

Use a very short fade rather than instantly changing volume to avoid clicks.

Impact ducking remains independent from normal volume modulation:

```text
Final music level =
    Base music level
  + Speed boost
  + Impact duck
```

This allows a crash to temporarily dominate the mix without changing the normal music state.

---

## 4. Unity Architecture

Keep the prototype implementation deliberately small.

```text
MusicDirector
│
├── Playback
│    ├── Intro
│    ├── Part A
│    └── Part B
│
├── Mix evaluation
│    ├── Speed → volume
│    └── Combo → bass EQ
│
├── Impact response
│    └── Temporary duck
│
└── AudioMixer
     └── Music
          ├── Base Volume
          └── Bass EQ
```

The director should expose a small public API along the lines of:

```csharp
public void StartStage(MusicTrack track);
public void StartStageFromPartA(MusicTrack track);
public void FinishStage();

public void OnImpact(float severity);
```

Speed, combo and other persistent gameplay information are obtained internally from the relevant systems.

---

## 5. Track Data

Avoid putting individual audio files and timings directly into `MusicDirector`.

Create a simple `ScriptableObject`, e.g. `MusicTrack`:

```csharp
[CreateAssetMenu(menuName = "Audio/Music Track")]
public class MusicTrack : ScriptableObject
{
    public AudioClip intro;
    public AudioClip partA;
    public AudioClip partB;
}
```

Each purchased song gets one `MusicTrack` asset.

This makes selecting/randomizing tracks between rally stages straightforward later.

---

## 6. Prototype Scope

Implement only:

1. `Intro → A → B → B...` DSP-scheduled playback.
2. Ability to begin directly from A after a breather stop.
3. Stage-end fade-out.
4. Periodic speed-based volume adjustment.
5. Periodic combo/skill-based bass EQ adjustment.
6. Event-driven impact ducking.
7. `MusicTrack` assets for configuring songs.

Do **not** initially implement:

- Dynamic A/B selection.
- Generic intensity values.
- Stem mixing.
- Playback-speed/pitch modulation.
- Automatic beat/bar detection.
- Complex music state machines.

## Future Upgrade Path

If the prototype warrants a more sophisticated soundtrack, the same `MusicDirector` can later consume:

- Original stems obtained from the Race Tracks composer.
- Construction-kit music with separate drums/bass/synth/FX.
- Additional A/B sections.
- Gameplay-controlled stem layering.

The gameplay-facing architecture can remain largely unchanged; the additional complexity stays inside the music system.