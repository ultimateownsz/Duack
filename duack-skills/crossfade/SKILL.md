---
name: crossfade
description: "Duack crossfades and scene transitions — sequential crossfades between any number of tracks, desktop (overlap) vs web (sequenced) backends, playlists, ducking during transitions, hard-reset vs natural-gate patterns. Triggers on: crossfade, scene transition, MusicManager.Play, ReleaseDucks, IsDuckReady, sound effects (SFX) ducking delay."
---

# Duack — Crossfades and Scene Transitions

## 1. Overview

Duack supports **sequential crossfades between any number of tracks**. You do not need to
pre-queue all tracks — each call to `MusicManager.Play()` triggers an independent crossfade
from whatever is currently playing to the new track.


- Judge a transition by how it felt at its worst moment and its final moment, not by the average.
- A hard cut between tracks creates a jarring peak (silence gap) that players remember; an
equal-power crossfade smooths the peak so the transition reads as seamless. The ear notices a
gap instantly, so keep `CrossfadeSeconds` at or below 1.5 s for scene switches and reserve
longer fades (2-4 s) for deliberate mood shifts.

### Runnable reference: the crossfade sample

`samples/Duack.Sample.Crossfade` is a complete KNI / Blazor WASM project that wires
crossfades, playlists, and the transition telemetry end to end: track selection,
4s/8s/12s/16s crossfade presets, a live toggle (crossfade vs hard cut), and a
transition meter driven by `IsTransitioning` / `TransitionElapsedSeconds` /
`TransitionDurationSeconds`. Read its source before hand-authoring web wiring —
the backend construction (`MediaPlayerMusicBackend(new KniMediaPlayerHost(),
contentRoot: "Content/Audio/Tracks", extension: ".ogg")`), the manifest-based
library, and the manifest → playlist flow are all in that sample. The sample is not part of
`Duack.slnx` (build it explicitly); `tests/Duack.Sample.Tests` pins the shared
`Duack.Sample.Core`, not this web head.

The same demo runs in the browser (GitHub Pages): the crossfade demo listed in the README's samples table. Browsers suspend audio until a user gesture, so the demo starts on a click.

## 2. Basic usage

```csharp
var playlists = new PlaylistConfig();
playlists.Playlists["overworld"] = new PlaylistDefinition
{
    Tracks = { new PlaylistTrack { Name = "overworld", Context = "Overworld", FadeInSeconds = 0.4f, FadeOutSeconds = 0.25f } },
    Repeat = PlaylistRepeatMode.Track,
};
playlists.Playlists["battle"] = new PlaylistDefinition
{
    Tracks = { new PlaylistTrack { Name = "battle", Context = "Battle", FadeInSeconds = 0.4f, FadeOutSeconds = 0.25f } },
    Repeat = PlaylistRepeatMode.Track,
};
playlists.Playlists["hospital"] = new PlaylistDefinition
{
    Tracks = { new PlaylistTrack { Name = "hospital", Context = "Overworld", FadeInSeconds = 0.4f, FadeOutSeconds = 0.25f } },
    Repeat = PlaylistRepeatMode.Track,
};

var config = new AudioConfigBuilder()
    .WithCrossfadeSeconds(1.5f)
    .Build();

var manager = new MusicManager(backend, new MusicPlaylistController(playlists), config.Music);

manager.Play("overworld");  // uses AudioConfig.Music.CrossfadeSeconds + track fades
manager.Stop(fadeOutSeconds: 0.5f);

// Each Play() is an independent crossfade:
manager.Play("overworld");   // overworld track plays (loops)
manager.Play("battle");      // crossfade overworld → battle (1.5s)
manager.Play("hospital");    // crossfade battle → hospital (1.5s)
manager.Play("overworld");   // crossfade hospital → overworld (1.5s)
```

## 3. How crossfades work

### Desktop (MonoGame) — `SupportsOverlap = true`

Two tracks overlap during the crossfade:
1. Current track demoted to "outgoing" (fading out)
2. New track starts at volume 0 (fading in)
3. Both volumes follow the equal-power curve (`CrossfadeMath.EqualPower`)
4. After `CrossfadeSeconds`, outgoing is stopped and disposed

If a new `Play()` arrives mid-crossfade, the old outgoing is stopped and the current active
becomes the new outgoing. The new track starts at volume 0 and crossfades in.

### Web (KNI) — `SupportsOverlap = false`

Single-track player (HTML `<audio>` / MediaPlayer). Transition is sequenced:
1. Current track fades out over `CrossfadeSeconds / 2`
2. Back end swaps to the new track
3. New track fades in over `CrossfadeSeconds / 2`

Total wall-clock time is the same as desktop (`CrossfadeSeconds`).

### Transition telemetry (backend-agnostic)

`MusicManager` exposes the same transition timeline on both back ends, computed
differently under the hood but consumed identically:

- `IsTransitioning` — true while a crossfade is in flight
- `TransitionFromTrack` / `TransitionToTrack` — the two tracks in the current transition
  (not the playlist contents; membership lives in `PlaylistConfig` / the controller)
- `TransitionElapsedSeconds` / `TransitionDurationSeconds` — elapsed and total seconds for
  one timeline: on desktop it spans the overlap; on web it composes the fade-out and fade-in
  phases. Their ratio is a valid progress value on either backend.

```csharp
// Backend-agnostic progress gate: identical code on desktop and web.
if (manager.IsTransitioning)
{
    var progress = manager.TransitionElapsedSeconds / manager.TransitionDurationSeconds;
    // e.g. hold the old scene's visuals until the new track is audible
}
```

Telemetry does not remove the acoustic difference: desktop overlaps two live tracks,
web is still a single-instance player (fade-out → swap → fade-in). It removes the
observable difference — transition-gated logic writes once and runs on both.

## 4. Ducking during transitions

The `DuckMixer` handles side chain ducking independently. When an SFX plays, it pulses the
duck ratio. The duck applies to whatever track is currently active (including during
cross fades).

### Two complementary patterns

**Hard reset** — call `ReleaseDucks()` on scene switch:
```csharp
if (frame.Music is not null)
{
    manager.ReleaseDucks();  // immediately reset all duck sources
    manager.Play(frame.Music);
}
```
The duck recovers instantly (no easing tail), so the incoming track starts at full volume
from frame one. Best when you want the incoming track at full volume immediately.

**Natural gate** — skip `ReleaseDucks()` and gate the visual transition on `IsDuckReady()`:
```csharp
if (frame.Music is not null)
{
    manager.Play(frame.Music);  // crossfade starts immediately
}
// In Draw or Update, defer the visual scene change:
if (story.Scene != displayedScene && manager.IsDuckReady())
{
    displayedScene = story.Scene;  // only switch visuals once duck has recovered
}
```
The duck eases back over its release tau, and the visual changes once it has recovered.
Best when you want to preserve the duck's smooth release curve.

### Ducking API reference

| Method | Description |
|---|---|
| `MusicManager.Pulse(source, hold)` | Ducks a source for its configured hold |
| `MusicManager.SetDuck(source, active)` | Holds a source ducked on/off |
| `MusicManager.ReleaseDucks()` | Immediately releases all duck sources (no easing) |
| `MusicManager.IsDuckReady(threshold)` | True when combined duck ratio ≥ threshold (default 0.99) |
| `MusicManager.DuckRatio` | Current combined duck ratio |

## 5. Playlists for automatic sequencing

For multi-track sequences (e.g., battle intro → battle loop → victory):

```csharp
playlists.Playlists["battle-sequence"] = new PlaylistDefinition
{
    Tracks = {
        new PlaylistTrack { Name = "battle-intro", Context = "Battle" },
        new PlaylistTrack { Name = "battle-loop", Context = "Battle" },
        new PlaylistTrack { Name = "victory", Context = "Battle" },
    },
    Repeat = PlaylistRepeatMode.Off,   // stop after victory
    Shuffle = PlaylistShuffleMode.Off,  // play in order
};
manager.Play("battle-sequence"); // plays intro → loop → victory, crossfading between each
```

Tracks advance automatically when the current track finishes. `Repeat` and `Shuffle` control
the advancement behavior.

## 6. Scene transitions in practice

The desktop sample demonstrates the full pattern:

```csharp
private void ApplyFrame(StoryFrame frame)
{
    // Release ducks on scene switch so the incoming track starts clean
    if (frame.SceneChanged || frame.Music is not null)
        manager.ReleaseDucks();

    if (frame.Music is not null)
        manager.Play(frame.Music);  // triggers crossfade immediately

    // Cut lingering SFX on scene switch (e.g., long dialog cue)
    if (frame.Sfx == StoryController.BattleStartCue)
        sfx.Stop();

    if (frame.Sfx is not null)
    {
        // Duck only when no scene switch is happening
        var duck = frame.Music is null;
        sfx.Play(frame.Sfx, frame.Pitch, duck: duck);
    }
}
```

## 7. Clean transitions: no leftover audio on the new screen

A scene change should never let the previous track or SFX bleed into the new screen. Two
complementary tools:

1. **Cut lingering SFX** before playing the next one (and on every scene change):
   ```csharp
   if (frame.Sfx is not null) sfx.Stop();   // hard cut, previous one-shot never bleeds
   if (frame.SceneChanged) sfx.Stop();
   ```
2. **Music** — choose one of:
   - **Crossfade early**: start the new track during the outgoing screen's tail so it is
     already playing (and the old one gone) when the new screen appears. Example: on the
     battle-start cue, crossfade overworld→battle over the 1.2s intro:
     ```csharp
     if (frame.Sfx == BattleStartCue && manager.CurrentTrack?.Name != "battle")
         manager.Play("battle", fadeInSeconds: BattleIntroSeconds);
     ```
   - **Hard stop + fade in**: when an early crossfade is not possible, stop the old track
     instantly and fade the new one in:
     ```csharp
     if (frame.Music is not null && manager.CurrentTrack?.Name != frame.Music)
     {
         manager.Stop();                       // old track gone this frame
         manager.Play(frame.Music, fadeInSeconds: 0.8f);
     }
     ```

The `CurrentTrack?.Name` guard prevents re-triggering a track that is already playing.

## 8. Key insight: Play() triggers transition immediately

`MusicManager.Play()` calls `Update(0f)` internally, so the crossfade starts on the same
frame instead of waiting for the next `Update()` call. This ensures the audio track plays
right after the last action in the previous scene.

## 9. Limitations

- **Max 2 simultaneous tracks** (during a crossfade). You cannot have 3+ tracks playing at
  once. But sequential transitions are unlimited — chain as many `Play()` calls as needed.
- **No pre-queuing**: You call `Play()` when the transition should happen. For automatic
  multi-track sequencing, use `PlaylistConfig` with `Repeat` and `Shuffle`.
- **Crossfade duration**: Configured via `AudioConfigBuilder.WithCrossfadeSeconds(seconds)`.
  Default is 1.2s. Both desktop and web use the same duration.
- **Per-call overrides**: `Play(name, fadeInSeconds)` / `Stop(fadeOutSeconds)` override the config
  duration for that transition. Pass `null` (the default) to honour `AudioConfig.Music.CrossfadeSeconds`.
- **Track entry/exit fades**: Use `PlaylistTrack.FadeInSeconds` / `FadeOutSeconds` to smooth track starts/stops. `MusicManager` applies these for both overlap and sequenced backends (including web/KNI). Example: set `FadeInSeconds = 0.4f`, `FadeOutSeconds = 0.25f` on tracks for a polished feel.
- **Web (KNI)**: Single-track player (fade-out → swap → fade-in). Each transition costs
  `CrossfadeSeconds` total. The sequencing logic is identical to desktop. Always use Duack's C# API (`MusicManager`/`PlaylistTrack`) in games — do not hand-roll fades with raw browser `<audio>`/JS.
- **Ducking during crossfades**: The duck ratio applies to whatever track is active. During
  a crossfade, both outgoing and incoming tracks are affected by the current duck ratio. Use
  `ReleaseDucks()` before a scene switch to ensure the incoming track starts at full volume.
- **Dialog SFX overlap**: Long SFX (like dialog cues) should be cut with `sfx.Stop()` on
  scene switch to prevent audio overlap with the next track's crossfade.

## Studio & DAW concepts

- **Crossfade / Ramp**: In DAWs and mixing consoles, transitions between signals and automation ramps are drawn as fades/crossfades. Duack's equal-power crossfade curve preserves acoustic power across track transitions.
- **Fade up / Fade down**: Studio/broadcast verbs corresponding to track fade-in and fade-out automation.

## 9b. Minimal config-driven example (copy-paste starting point)

```csharp
// 1) Playlists: null fades => CrossfadeSeconds drives the transition
var playlists = new PlaylistConfigBuilder()
    .WithPlaylist("Overworld", p => p
        .Track("overworld", "Overworld", fadeInSeconds: null, fadeOutSeconds: null)
        .Repeat(PlaylistRepeatMode.Track))
    .Build();

// 2) Mix: one knob controls every transition
var config = new AudioConfigBuilder()
    .WithCrossfadeSeconds(2.0f)   // change here to tune all transitions
    .WithContext("Overworld", 1f)
    .Build();
```

Swap the track name/context and the crossfade seconds for your game; `null` overrides are what
make the single `WithCrossfadeSeconds` knob take effect.

## 10. API quick reference

| Method | Description |
|---|---|
| `MusicManager.Play(name)` | Start a playlist (triggers crossfade immediately if different track) |
| `MusicManager.Next()` | Skip to next track in current playlist |
| `MusicManager.Stop()` | Stop playback and clear all transition state |
| `MusicManager.Update(dt)` | Advance playback by dt seconds (call per frame) |
| `MusicManager.Pulse(source, hold)` | Duck a source for its configured hold |
| `MusicManager.SetDuck(source, active)` | Hold a source ducked on/off |
| `MusicManager.ReleaseDucks()` | Immediately release all duck sources |
| `MusicManager.IsDuckReady(threshold)` | True when duck ratio ≥ threshold |
| `MusicManager.DuckRatio` | Current combined duck ratio |
| `MusicManager.GainReduction` / `Attenuation` | Current combined gain reduction (1 − DuckRatio) |
| `MusicManager.CurrentTrack` | The track that should be playing |
| `MusicManager.IsPlaying` | True while a playlist is running |
| `MusicManager.IsTransitioning` | True while a crossfade is in flight |
| `MusicManager.TransitionFromTrack` | Track being faded out (null when none) |
| `MusicManager.TransitionToTrack` | Track being faded in (null when none) |
| `MusicManager.TransitionElapsedSeconds` | Elapsed seconds in the current transition |
| `MusicManager.TransitionDurationSeconds` | Total transition length (web: both phases) |
