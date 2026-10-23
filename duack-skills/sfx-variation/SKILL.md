---
name: sfx-variation
description: SFX variation in Duack — MarbleBag<T> draw-without-replacement randomizer, CueVariation pitch/volume jitter, CuePlay, and SfxManager cue playback with auto-ducking. Use when adding footstep/hit variation, avoiding back-to-back repeats, per-play pitch randomization, or one-shot SFX that duck music.
---

# Duack — SFX Variation

## MarbleBag<T>

Draw-without-replacement randomizer: every item appears once per cycle, then refills. Swap-remove
draw; refill on empty; optional avoid-immediate-repeat across cycle boundary; injectable `Random`
for determinism in tests.

```csharp
var bag = new MarbleBag<string>(cues, random, avoidImmediateRepeat: true);
var next = bag.Draw();          // throws if empty construction
bool ok = bag.TryDraw(out var cue); // false if no items
// bag.Count, bag.Remaining, bag.IsEmpty, bag.Refill()
```

- Duplicates allowed and counted separately.
- `avoidImmediateRepeat` (default false): a draw never returns the same item as the previous
  draw, including across a cycle boundary. Needs at least one different item.
- Empty bag: `Draw()` throws `InvalidOperationException`; `TryDraw` returns false.

## CueVariation

Per-play pitch/volume jitter for music cues and SFX.

```csharp
var variation = new CueVariation(pitchRange: 0.15f, volumeRange: 0.1f);
// pitchRange: half-width in engine pitch units (0..1, 1 = one octave)
// volumeRange: half-width as linear scale around 1 (0.2 = +/-20%)
CuePlay play = variation.Resolve(random);
// play.Pitch (clamped [-1,1]), play.VolumeScale (floored at 0)
```

`CueVariation.None` = no jitter. Non-finite/negative ranges become 0.

`MusicConfig.CueVariation` / `SfxConfig.CueVariation` (nullable; omitted from JSON when null).
`MusicManager` applies `NextCuePitch()` to track plays; `SfxManager` applies its own
`SfxConfig.CueVariation` per play (jitter added on top of the passed pitch, volume scaled).

## SfxManager

Plays one-shot cues by name against an `AudioLibrary`; auto-ducks music on success.

```csharp
var sfx = new SfxManager(library, backend, director, sfxConfig: new SfxConfig { CueVariation = variation });
IReadOnlyList<string> names = sfx.Names;
bool played = sfx.Play("hit", pitch: 0.25f, volume: 1f, duck: true); // jitter applied on top of the passed values
sfx.PlayNext(pitch, volume);   // marble-bag draw, no immediate repeats
sfx.Stop();                    // hard cut all one-shots
```

- Returns false (no backend call, no duck) when cue not in library or backend refuses.
- `duck: false` plays without ducking (e.g. scene-switch cues).
- Pitch passed through in engine units (MonoGame -1..1, 0 = natural); with an authored
  `SfxConfig.CueVariation` the resolved jitter is added on top, clamped to [-1, 1].
- Volume is the caller's value scaled by the authored jitter (volume half-width), then master × bus.

## SFX variation pattern (XACT successor)

A cue may declare multiple variants plus per-play pitch/volume ranges. Pure data + pure math
in core; adapters apply pitch at play time. Use `MarbleBag<T>` for variant selection so a
footstep/hit never repeats back-to-back.

Example — marble bag of hit pitches:
```csharp
var pitches = new MarbleBag<float>(new[] { -0.1f, 0f, 0.1f, 0.25f }, random, avoidImmediateRepeat: true);
sfx.Play("hit", pitch: pitches.Draw());
```

## Backends

- Desktop: `SoundEffectSfxBackend` (IAudioClipLibrary) — non-looping instance per cue, pitch/volume
  clamped to engine ranges, finished instances reaped on next play, `Dispose` stops everything.
- Web: no SFX backend yet (music-only); use `director.Pulse("sfx")` for ducking.

## ISfxBackend

```csharp
public interface ISfxBackend
{
    bool Play(string cueName, float pitch, float volume);
    void Stop();   // hard-stop all one-shots
}
```

## Studio & sound design concepts

- **MarbleBag variation**: Corresponds to **round-robin** cycling in professional sampler libraries (cycling articulations or shuffled round-robin to avoid the "machine-gun effect" of repetitive sample playback).
- **One-shot**: Native sampler terminology for a sample triggered from start to finish without looping.
- **Cue**: Production cue triggering (the theatrical/game production sense of firing sound assets).

See also: `audio-library`, `ducking`, `audio-core`.
