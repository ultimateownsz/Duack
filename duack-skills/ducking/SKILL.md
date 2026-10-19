---
name: ducking
description: Sidechain ducking in Duack — DuckMixer, named duck sources (sfx, dialogue), attack/hold/release, Pulse/SetDuck/ReleaseDucks/IsDuckReady, hot-reload-safe ReloadSources, and gating scene transitions on duck recovery. Use when implementing music ducking, sidechain compression, dialogue ducking, or scene-switch duck handling.
---

# Duack — Sidechain Ducking

Duack ducks music when SFX/dialogue play, using a `DuckMixer` of named sources. Each source
eases toward its target ratio with an exponential time constant (attack when ducking down,
release when coming back up); active sources combine multiplicatively.

## DuckSourceConfig

```csharp
public class DuckSourceConfig
{
    public float Ratio { get; set; } = 0.3f;          // target music ratio while active (0=silent,1=no duck)
    public float AttackSeconds { get; set; } = 0.06f; // time constant easing down
    public float HoldSeconds { get; set; } = 0f;      // how long to stay ducked; 0 = release immediately
    public float ReleaseSeconds { get; set; } = 0.35f;// time constant easing back up
}
```

Configured under `MusicConfig.DuckSources` (keyed by source name: "sfx", "dialogue", ...).

## DuckMixer API

| Method | Purpose |
|---|---|
| `SetSource(name, config)` | Register/replace a source. |
| `ReloadSources(IEnumerable<KVP>)` | Hot-reload: swaps params in place, preserves active/pulse/current-ratio; adds new, drops removed. |
| `Set(name, active)` | Hold a source ducked on/off (dialogue open/close). |
| `Pulse(name, holdSeconds = -1)` | Duck for hold (or configured hold), then release. |
| `ReleaseAll()` | Immediately release every source (no easing tail). |
| `IsReady(threshold = 0.99)` | True when combined ratio >= threshold. |
| `Update(deltaSeconds)` | Advance all sources; call per frame. |
| `EffectiveRatio` | Combined ratio applied to music (1 = no ducking). |

## MusicDirector ducking facade

```csharp
director.Pulse("sfx");                 // duck for configured hold
director.Pulse("sfx", 0.5f);           // duck for 0.5s
director.SetDuck("dialogue", true);    // hold ducked
director.SetDuck("dialogue", false);   // release
director.ReleaseDucks();               // instant release (scene switch)
director.IsDuckReady(0.99f);           // recovered?
director.DuckRatio;                    // current combined ratio
```

## Typical patterns

### SFX auto-duck (SfxDirector)
`SfxDirector.Play` pulses the configured duck source (default "sfx") on every successful play.
Unknown cues / backend refusals never duck.

### Dialogue ducking
```csharp
// dialogue starts
director.SetDuck("dialogue", true);
// dialogue ends
director.SetDuck("dialogue", false);
```

### Scene switch — two complementary patterns

**Hard reset** — instant recovery, incoming track at full volume from frame one:
```csharp
if (frame.Music is not null)
{
    director.ReleaseDucks();   // instantly reset all sources
    director.Play(frame.Music);
}
```

**Natural gate** — preserve smooth release curve, defer visuals:
```csharp
if (frame.Music is not null) director.Play(frame.Music);
// in Update/Draw:
if (story.Scene != displayedScene && director.IsDuckReady())
    displayedScene = story.Scene;
```

## Composition

Final music volume = `MusicVolumeMath.Effective(master, contextScale, duckRatio, fade)`,
clamped [0,1]. Duck ratio multiplies whatever track is active, including during crossfades.

## Hot reload

After re-parsing config, rebuild duck sources without losing state:
```csharp
var newSources = config.Music.DuckSources;   // IEnumerable<KVP>
// DuckMixer.ReloadSources(newSources) — or rebuild MusicDirector and call ReloadSources on its mixer
```
`ReloadSources` keeps active/pulse/current-ratio continuous; new sources added, removed dropped.

## Tips

- Short attack (60ms) pulses fired mid-crossfade must not glitch — test on web.
- `ReleaseAll` snaps `CurrentRatio` to 1 and `EffectiveRatio` to 1 (no easing tail).
- `IsReady` defaults to 0.99 threshold (~inaudible duck).

See also: `crossfade` (ducking during transitions), `audio-engine`.
