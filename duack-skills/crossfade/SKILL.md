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
    Tracks = { new PlaylistTrack { Name = "overworld", Context = "Overworld", FadeInSeconds = null, FadeOutSeconds = null } },
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

var manager = new MusicManager(backend, new MusicPlaylistController(playlists), config.Music);

manager.Play("overworld");  // uses AudioConfig.Music.CrossfadeSeconds
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
- **Per-call overrides**: `Play(name, fadeInSeconds)` / `Stop(fadeOutSeconds)` override the config
  duration for that transition. Pass `null` (the default) to honour `AudioConfig.Music.CrossfadeSeconds`.
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

## 8b. Minimal config-driven example (copy-paste starting point)

```csharp
// 1) Playlists: null fades => CrossfadeSeconds drives the transition
builder.WithPlaylist("Overworld", p => p
    .Track("overworld", "Overworld", fadeInSeconds: null, fadeOutSeconds: null)
    .Repeat(PlaylistRepeatMode.Track));

// 2) Mix: one knob controls every transition
new AudioConfigBuilder()
    .WithCrossfadeSeconds(2.0f)   // change here to tune all transitions
    .WithContext("Overworld", 1f)
    .Build();
```

Swap the track name/context and the crossfade seconds for your game; `null` overrides are what
make the single `WithCrossfadeSeconds` knob take effect.

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
