---
name: crossfade
description: Duack crossfades and scene transitions — sequential crossfades between any number of tracks, desktop (overlap) vs web (sequenced) backends, playlists, ducking during transitions, hard-reset vs natural-gate patterns. Triggers on: crossfade, scene transition, MusicManager.Play, ReleaseDucks, IsDuckReady, overworld, battle, hospital, ducking delay.
---

# Duack — Crossfades and Scene Transitions

## 1. Overview

Duack supports **sequential crossfades between any number of tracks**. You do not need to
pre-queue all tracks — each call to `MusicManager.Play()` triggers an independent crossfade
from whatever is currently playing to the new track.

## 2. Basic usage

```csharp
var playlists = new PlaylistConfig();
playlists.Playlists["overworld"] = new PlaylistDefinition
{
    Tracks = { new PlaylistTrack { Name = "overworld", Context = "Overworld" } },
    Repeat = PlaylistRepeatMode.Track,
};
playlists.Playlists["battle"] = new PlaylistDefinition
{
    Tracks = { new PlaylistTrack { Name = "battle", Context = "Battle" } },
    Repeat = PlaylistRepeatMode.Track,
};
playlists.Playlists["hospital"] = new PlaylistDefinition
{
    Tracks = { new PlaylistTrack { Name = "hospital", Context = "Overworld" } },
    Repeat = PlaylistRepeatMode.Track,
};

var config = new AudioConfigBuilder()
    .WithCrossfadeSeconds(1.5f)
    .WithPlaylists(playlists)
    .Build();

var director = new MusicManager(backend, new MusicPlaylistController(playlists), config.Music);

director.Play("overworld", fadeInSeconds: 1.0f);
director.Stop(fadeOutSeconds: 0.5f);

// Each Play() is an independent crossfade:
director.Play("overworld");   // overworld track plays (loops)
director.Play("battle");      // crossfade overworld → battle (1.5s)
director.Play("hospital");    // crossfade battle → hospital (1.5s)
director.Play("overworld");   // crossfade hospital → overworld (1.5s)
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
2. Backend swaps to the new track
3. New track fades in over `CrossfadeSeconds / 2`

Total wall-clock time is the same as desktop (`CrossfadeSeconds`).

## 4. Ducking during transitions

The `DuckMixer` handles sidechain ducking independently. When an SFX plays, it pulses the
duck ratio. The duck applies to whatever track is currently active (including during
crossfades).

### Two complementary patterns

**Hard reset** — call `ReleaseDucks()` on scene switch:
```csharp
if (frame.Music is not null)
{
    director.ReleaseDucks();  // immediately reset all duck sources
    director.Play(frame.Music);
}
```
The duck recovers instantly (no easing tail), so the incoming track starts at full volume
from frame one. Best when you want the incoming track at full volume immediately.

**Natural gate** — skip `ReleaseDucks()` and gate the visual transition on `IsDuckReady()`:
```csharp
if (frame.Music is not null)
{
    director.Play(frame.Music);  // crossfade starts immediately
}
// In Draw or Update, defer the visual scene change:
if (story.Scene != displayedScene && director.IsDuckReady())
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
director.Play("battle-sequence"); // plays intro → loop → victory, crossfading between each
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
        director.ReleaseDucks();

    if (frame.Music is not null)
        director.Play(frame.Music);  // triggers crossfade immediately

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

## 7. Key insight: Play() triggers transition immediately

`MusicManager.Play()` calls `Update(0f)` internally, so the crossfade starts on the same
frame instead of waiting for the next `Update()` call. This ensures the audio track plays
right after the last action in the previous scene.

## 8. Limitations

- **Max 2 simultaneous tracks** (during a crossfade). You cannot have 3+ tracks playing at
  once. But sequential transitions are unlimited — chain as many `Play()` calls as needed.
- **No pre-queuing**: You call `Play()` when the transition should happen. For automatic
  multi-track sequencing, use `PlaylistConfig` with `Repeat` and `Shuffle`.
- **Crossfade duration**: Configured via `AudioConfigBuilder.WithCrossfadeSeconds(seconds)`.
  Default is 1.2s. Both desktop and web use the same duration.
- **Web (KNI)**: Single-track player (fade-out → swap → fade-in). Each transition costs
  `CrossfadeSeconds` total. The sequencing logic is identical to desktop.
- **Ducking during crossfades**: The duck ratio applies to whatever track is active. During
  a crossfade, both outgoing and incoming tracks are affected by the current duck ratio. Use
  `ReleaseDucks()` before a scene switch to ensure the incoming track starts at full volume.
- **Dialog SFX overlap**: Long SFX (like dialog cues) should be cut with `sfx.Stop()` on
  scene switch to prevent audio overlap with the next track's crossfade.

## Studio & DAW concepts

- **Crossfade / Ramp**: In DAWs and mixing consoles, transitions between signals and automation ramps are drawn as fades/crossfades. Duack's equal-power crossfade curve preserves acoustic power across track transitions.
- **Fade up / Fade down**: Studio/broadcast verbs corresponding to track fade-in and fade-out automation.

## 9. API quick reference

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
