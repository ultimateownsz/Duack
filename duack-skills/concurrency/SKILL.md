---
name: concurrency
description: Voice concurrency limiting in Duack - global and per-cue caps, oldest/quietest/priority stealing policies, PlayDetailed refusal reasons, active-voice readback, Update(dt) lifetime, and the Desktop stress sample. Use when bounding simultaneous SFX, choosing a stealing policy, diagnosing silently dropped cues, or reading the active-voice counter.
---

# Duack — Voice Concurrency

Duack caps **SFX voices only**. Music is already bounded at two tracks by the crossfade design.
A cap is opt-in: `GlobalMaxVoices = 0` (the default) means unlimited. When a cap is reached the
limiter either steals an active voice or refuses the new play; either way the outcome is
observable, so no cue disappears silently.

## Authoring caps

```csharp
var config = new AudioConfigBuilder()
    .WithVoiceLimit(globalMaxVoices: 8, defaultVoiceSeconds: 0.5f,
                    defaultStealingPolicy: VoiceStealingPolicy.Oldest)
    .WithCueVoiceLimit("hit", new CueVoiceLimit
    {
        MaxVoices = 2,            // per-cue cap overrides the global cap
        Priority = 7,             // used only by the Priority policy
        DurationSeconds = 0.25f,  // overrides the default assumed lifetime
        StealingPolicy = VoiceStealingPolicy.Oldest,
    })
    .Build();

var sfx = new SfxManager(library, backend, music, sfxConfig: config.Sfx);
```

`SfxConfig.VoiceLimits` carries the same graph for hosts that assemble `AudioConfig` by hand; the
`voiceLimits:` constructor argument takes precedence when both are supplied.

## Policies

| Policy | Victim when full |
|---|---|
| `Oldest` (default) | The voice that started first. |
| `Quietest` | Lowest final playback volume; oldest wins ties. |
| `Priority` | Weakest authored priority; a new voice at or below it is refused. |

## Reporting and readback

```csharp
var result = sfx.PlayDetailed("hit", priority: 7);
// SfxPlayResult.Played | UnknownCue | RejectedBelowPriority | BackendRefused
var reason = sfx.LastPlayResult;   // also set by plain Play(...)
var active = sfx.ActiveVoiceCount; // for HUD counters / diagnostics
```

`Play(...)` keeps its historical `bool` return (true only for `Played`). A backend refusal
releases the reserved slot immediately, so it does not consume capacity.

## Lifetime and unscaled time

Backends expose no voice-end event, so the limiter **ages** voices with an authored lifetime
(`DefaultVoiceSeconds`, per-cue `DurationSeconds`) advanced by:

```csharp
sfx.Update(unscaledDeltaSeconds); // every frame; never scaled time
```

This is the same unscaled-time rule as fades and ducks: scaled time would keep a voice alive
through a slow-motion effect and starve real cues. The default is deliberately short (0.5 s);
tune it to the actual length of the cues you cap. Exact voice-end tracking needs playback handles,
which are a deferred experiment.

## Concurrency example

`samples/Duack.Sample.Concurrency` is the dedicated concurrency example — the storyline samples are
deliberately left untouched. Run it with
`dotnet run --project samples/Duack.Sample.Concurrency` (it reuses the desktop sample's SFX
assets via the project file). Click **PLAY** for a single cue, **MANY** for 50 overlapping cues
through a manager capped at 8 voices, or **STOP** to silence everything mid-listen (Space/T and
the keys mirror the buttons). The two-digit readout and the eight slot bars show
`ActiveVoiceCount`; the count must never exceed 08 during the burst. A human listening check is
still required before voice limiting is called done; the counter alone is not proof of audible quality.
