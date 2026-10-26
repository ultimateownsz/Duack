---
name: playlists
description: Playlists in Duack — PlaylistConfig, PlaylistDefinition, PlaylistTrack, Repeat/Shuffle modes, MusicPlaylistController sequencing, bag-style shuffle via MarbleBag, auto-build from AudioLibrary, and crossfade integration. Use when authoring multi-track sequences, shuffle, loop behavior, or playlist-driven music.
---

# Duack — Playlists

Playlists are backend-agnostic: `MusicPlaylistController` owns sequencing decisions; adapters
only report completion. Never use engine `MediaQueue`/`MediaPlayer` playlist state (not
modifiable at runtime, absent on web).

## Data model

```csharp
public class PlaylistTrack { string Name; string Context; float? FadeInSeconds; float? FadeOutSeconds; }
public class PlaylistDefinition
{
    List<PlaylistTrack> Tracks;
    PlaylistRepeatMode Repeat;   // Off, Playlist, Track
    PlaylistShuffleMode Shuffle; // Off, Bag
}
public class PlaylistConfig { Dictionary<string, PlaylistDefinition> Playlists; bool TryGet(name, out def); }
```

## Modes

- `PlaylistRepeatMode.Off` — stop after last track (or shuffle cycle).
- `PlaylistRepeatMode.Playlist` — loop whole playlist forever.
- `PlaylistRepeatMode.Track` — loop current track forever until skipped.
- `PlaylistShuffleMode.Off` — authored order.
- `PlaylistShuffleMode.Bag` — `MarbleBag<int>`: every track once per cycle, random order, no
  back-to-back repeat across cycle boundary.

## Authoring in code

```csharp
var playlists = new PlaylistConfigBuilder()
    .WithPlaylist("battle-sequence", p => p
        .Track("battle-intro", "Battle")
        .Track("battle-loop",  "Battle")
        .Track("victory",      "Battle")
        .Repeat(PlaylistRepeatMode.Off)
        .Shuffle(PlaylistShuffleMode.Off))
    .WithPlaylist("overworld", p => p
        .Track("overworld", "Overworld", fadeInSeconds: 1.0f, fadeOutSeconds: 0.5f)
        .Repeat(PlaylistRepeatMode.Track))
    .Build();
```

## Controller

```csharp
var controller = new MusicPlaylistController(playlists, random /*optional*/);
var manager = new MusicManager(backend, controller, config.Music);

manager.Play("battle-sequence");                // starts from top, triggers crossfade
manager.Play("battle-sequence", fadeInSeconds: 1.0f); // optional fade in
manager.Next();                                // skip track (breaks track-repeat)
manager.Stop();                                // stop, no TrackCompleted event
manager.Stop(fadeOutSeconds: 0.5f);            // stop with fade out
```

Properties: `IsPlaying`, `CurrentPlaylistName`, `CurrentTrack`.

Events: `TrackCompleted` (raised after advance; handler sees successor or null).

## Completion flow

Adapter fires `IMusicBackend.TrackEnded` when active track ends → `MusicManager` calls
`controller.NotifyTrackCompleted()` → controller advances per repeat/shuffle → raises
`TrackCompleted` → manager begins transition (crossfade) on next `Update`.

`Repeat=Track` loops the same track: manager restarts playback itself (fade-out→swap→fade-in
on web, or restart with fade-in on desktop overlap path).

## Auto-build from AudioLibrary

```csharp
var library = AudioLibrary.ScanDirectory("Content/Audio");
var playlists = library.BuildPlaylists(new AudioLibraryPlaylistOptions
{
    AllTracksPlaylistName = "music",          // bag-shuffled all-tracks playlist
    SingleTrackRepeat = PlaylistRepeatMode.Track,
    AllTracksRepeat = PlaylistRepeatMode.Playlist,
    AllTracksShuffle = PlaylistShuffleMode.Bag,
    ContextForTrack = name => name == "battle" ? "Battle" : "Overworld"
});
```

Produces one single-track looping playlist per discovered track (addressed by track name) plus
an all-tracks bag-shuffled playlist (default name "music"). Name collision with a track throws.

Also `library.ToPlaylist(context, repeat, shuffle)` for a single looping playlist of all tracks.

## Embedding in config (JSON round-trip)

```csharp
new AudioConfigBuilder().WithPlaylists(playlists).Build();
// emits playlists section; read back via PlaylistJson / PlaylistConfigJsonContext
```

Playlists have their own source-generated context (`Duack.PlaylistJson`) — AOT/WASM safe.

## Crossfade integration

Playlist advancement applies the existing crossfade curve (`CrossfadeSeconds`). Ducking stays
audible and correct mid-transition. See `crossfade`.

## Edge cases

- Unknown/empty playlist → controller stops and stays idle (no throw).
- `Play(null)` throws `ArgumentNullException`.
- Empty library → `BuildPlaylists` yields no playlists.

## Studio & broadcast concepts

- **Playlist**: The playlist concept comes from broadcast and sequencing (radio rotations / broadcast playlists, whereas DAWs historically use *sessions*). Duack retains `Playlist` as the explicit term for authoring multi-track loops and rotations.
- **Context**: Represents the **scene / snapshot** or saved console mix state (similar to scene memory on digital mixing consoles like Yamaha/X32, or snapshots in DAWs/mixers). Each track or game situation declares its context (e.g., `"Overworld"`, `"Battle"`), scaling music levels according to the active snapshot.

See also: `crossfade`, `audio-library`, `audio-core`.
