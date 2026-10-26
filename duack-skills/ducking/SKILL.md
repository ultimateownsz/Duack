---
name: ducking
description: Sidechain ducking in Duack - DuckMixer, named duck sources (sfx, dialogue), attack/hold/release, Pulse/SetDuck/ReleaseDucks/IsDuckReady, ReloadSources (host-driven), and gating scene transitions on duck recovery. Use when implementing music ducking, sidechain compression, dialogue ducking, or scene-switch duck handling.
---

# Duack — Sidechain Ducking

Duack ducks music when SFX/dialogue play, using a `DuckMixer` of named sources. Each source
eases toward its target ratio with an exponential time constant (attack when ducking down,
release when coming back up); active sources combine multiplicatively. Ducks apply to the
music bus, under the master — the master and SFX bus are unaffected.

### Why duck? (UX rationale)

Ducking exists because of the **Doherty Threshold** (lawsofux.com/doherty-threshold): when
dialogue or an SFX cue plays, the music must dip within ~100 ms or the player perceives a
clash (two competing signals). Duack's exponential attack (default 60 ms) keeps the dip
under that threshold. The **Peak-End Rule** (lawsofux.com/peak-end-rule) also applies: a
smooth release tail (default 0.35 s hold + exponential release) reads as intentional
sidechain compression, while an instant cut reads as a bug. Use `ReleaseDucks()` for scene
switches where you want the incoming track at full volume immediately (hard reset), and
`IsDuckReady()` to gate visual transitions on duck recovery (natural gate).

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
| `ReloadSources(IEnumerable<KVP>)` | Host-driven: swaps params in place, preserves active/pulse/current-ratio; adds new, drops removed. |
| `Set(name, active)` | Hold a source ducked on/off (dialogue open/close). |
| `Pulse(name, holdSeconds = -1)` | Duck for hold (or configured hold), then release. |
| `ReleaseAll()` | Immediately release every source (no easing tail). |
| `IsReady(threshold = 0.99)` | True when combined ratio >= threshold. |
| `Update(deltaSeconds)` | Advance all sources; call per frame. |
| `EffectiveRatio` | Combined ratio applied to music (1 = no ducking). |
| `GainReduction` / `Attenuation` | Combined gain reduction / attenuation applied to music ($1 - \text{EffectiveRatio}$). |

## MusicManager ducking facade

```csharp
director.Pulse("sfx");                 // duck for configured hold
director.Pulse("sfx", 0.5f);           // duck for 0.5s
director.SetDuck("dialogue", true);    // hold ducked
director.SetDuck("dialogue", false);   // release
director.ReleaseDucks();               // instant release (scene switch)
director.IsDuckReady(0.99f);           // recovered?
director.DuckRatio;                    // current combined ratio
director.GainReduction;                // current gain reduction (1 - DuckRatio)
```

## Typical patterns

### SFX auto-duck (SfxManager)
`SfxManager.Play` pulses the configured duck source (default "sfx") on every successful play.
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

## Hot reload (host integration)

Duack has **no built-in file watching**. A host that wants live editing can re-parse the
config and rebuild duck sources without losing state:
```csharp
var newSources = config.Music.DuckSources;   // IEnumerable<KVP>
// DuckMixer.ReloadSources(newSources) — or rebuild MusicManager and call ReloadSources on its mixer
```
`ReloadSources` keeps active/pulse/current-ratio continuous; new sources added, removed dropped.

## Tips

- Short attack (60ms) pulses fired mid-crossfade must not glitch — test on web.
- `ReleaseAll` snaps `CurrentRatio` to 1 and `EffectiveRatio` to 1 (no easing tail).
- `IsReady` defaults to 0.99 threshold (~inaudible duck).

## Studio & sound engineering concepts

Duack's ducking model maps directly to classical audio engineering:
- **Duck source**: Sidechain compressor (a compressor triggered by an external sidechain input signal such as dialogue or SFX).
- **Attack, hold, release**: Verbatim compressor envelope timing parameters (attack time constant easing down, hold window, release time constant easing back up).
- **Ratio**: Compressor ratio ($n:1$) determining target gain reduction / attenuation while triggered.
- **Attenuation / Gain Reduction (GR)**: The effective reduction in amplitude applied to the music bus.

See also: `crossfade` (ducking during transitions), `audio-core`.
