---
name: audio-core
description: "Duack audio library — core types, namespaces, quick reference. Use when working with Duack's audio mixing, ducking, crossfades, playlists, SFX variation, player settings, or the two-directory audio convention. Triggers on: Duack, DuckMixer, MusicManager, MusicVolumeMath, AudioConfig, SfxManager, MarbleBag, AudioLibrary, ISfxBackend, IMusicBackend, PlayerSettings."
---

# Duack — Audio Library for XNA-related libraries

Duack is a config-driven audio mixer for MonoGame, KNI, FlatRedBall2 on desktop and
web. It adds what engine audio APIs don't ship: sidechain ducking, per-context music volumes,
crossfades, playlists, player-facing volume settings with persistence, and per-cue SFX
variation. Duack owns the mix and produces numbers; the user's engine owns playback.

**Status**: preview. Verified against 0.1.0-preview.4 (see CHANGELOG for the current
version). API may change between preview versions; check CHANGELOG's migration notes
before applying skill snippets to a newer package.

## Hard architectural rules

- `src/Duack` is **pure BCL** (`System.Text.Json` only). Zero engine/XNA/MonoGame/KNI references.
  Never add a package or project reference to it.
- Backend glue lives in adapter packages: `Duack.MonoGame` (desktop) and `Duack.KNI` (web).
- **This preview is code-first.** The configuration surface is the C# API. The core carries
  `PlaylistJson` for the standalone playlist wire format (tooling/runtime). No authored
  config file ships with the library and none is required at runtime.
- Playlists have their own source-generated context (`Duack.PlaylistJson`), AOT-safe for WASM.
- Hot reload is **not built in**: hosts can re-parse config and call
  `MusicManager.ReloadDuckSources()` to swap duck params in place (duck state preserved);
  file watching is the host's responsibility.

## Getting started (5 minutes)

### 1. Install
```sh
dotnet add package Duack --prerelease
dotnet add package Duack.MonoGame --prerelease   # desktop (MonoGame/KNI desktop)
dotnet add package Duack.KNI --prerelease        # browser/WASM
```

### 2. Organize audio files
```
Content/Audio/
  SFX/       # one-shots (wav preferred)
    hit.wav
    notice.ogg
  Tracks/    # loops/songs (ogg preferred)
    overworld.ogg
    battle.ogg
```
Drop files in - no cue registration. Classification is case-sensitive (`SFX`/`Tracks`, lowercase extensions).

### 3. Wire up (desktop, MonoGame)
```csharp
using Duack;
using Duack.MonoGame;

var library   = AudioLibrary.ScanDirectory(audioRoot);   // SFX/ + Tracks/ convention, no cue registration
var playlists = library.BuildPlaylists();    // one looping playlist per track, plus a bag-shuffled "music" playlist
var clips     = new SoundEffectClipLibrary(library);

var music = new MusicManager(
    new SoundEffectMusicBackend(clips),
    new MusicPlaylistController(playlists),
    config.Music, masterVolume: config.MasterVolume);

var sfx = new SfxManager(library,
    new SoundEffectSfxBackend(clips), music,
    masterVolume: config.MasterVolume,
    sfxBusVolume: config.Sfx.MasterVolume,
    sfxConfig: config.Sfx);

music.Play("overworld");          // playlist lookup: BuildPlaylists() named this single-track playlist after the track
sfx.Play("notice", duck: true);   // plays the "notice" cue; the "sfx" duck source dips the music automatically

// once per frame:
music.Update(dt);
```

### 4. Wire up (web, KNI)
```csharp
using Duack;
using Duack.KNI;

var manifest = AudioLibraryManifestJson.Deserialize(manifestJson); // duackcli scan writes audio-manifest.json
var library = AudioLibrary.FromManifest(manifest);
var playlists = library.BuildPlaylists();    // same auto playlists as desktop

var music = new MusicManager(
    // contentRoot must point at Tracks/ — the backend default (Content/Audio) predates the
    // two-directory convention and 404s under the standard layout.
    new MediaPlayerMusicBackend(new KniMediaPlayerHost(), contentRoot: "Content/Audio/Tracks"),
    new MusicPlaylistController(playlists),
    config.Music, masterVolume: config.MasterVolume);

// One-shot cues: WebAudio, URLs relative to the app root.
var sfxUrls = library.Sfx.Select(cue => new KeyValuePair<string, string>(cue, $"Content/Audio/SFX/{cue}.wav"));
var sfx = new SfxManager(library, new WebAudioSfxBackend(new WebAudioNative(), sfxUrls), music,
    masterVolume: config.MasterVolume,
    sfxBusVolume: config.Sfx.MasterVolume,
    sfxConfig: config.Sfx);

// music.Update(dt) once per frame, as on desktop.
```

### 5. Generate the web manifest
```sh
duackcli scan Content/Audio
```

### 6. Convert WAV masters to OGG for web
```sh
duackcli convert Content/Audio/_masters Content/Audio/Tracks --all
```
Already-converted files are skipped automatically.

### Common pitfalls
- **Missing `audio-manifest.json` on web** - run `duackcli scan` and check it in.
- **Case sensitivity** - `SFX`/`Tracks` and lowercase extensions required; web hosts 404 on mismatch.
- **OGG decode (KNI)** - KNI's `SoundEffect.FromStream` parses WAV only. See the NVorbis `TotalSamples` pitfall in `adapters`.
- **No audio on desktop** - ensure `Content/Audio/` is copied to build output.
- **Silent no-op ducking** - a `duck: true` play whose duck source is missing from
  `MusicConfig.DuckSources` does nothing silently. Verify the `SfxManager` duckSource
  name (default "sfx") is declared in the config.

See `adapters`, `audio-library`, `crossfade`, `playlists` for deeper coverage.

## Namespace migration

All types live in `Duack` (core), `Duack.MonoGame` (desktop adapter), `Duack.KNI` (web adapter).
Every namespace → `Duack`.

## Core types quick reference

### Mixing
- `DuckMixer` — sidechain ducking with attack/hold/release per named source
- `MusicVolumeMath` — effective volume math (master × context × duck × fade)
- `CrossfadeMath` — equal-power crossfade curve
- `MusicManager` — owns playlist sequencing, crossfade realization, ducking, context scales
- `MusicTransitionSequencer` — deferred fade-out → swap → fade-in for single-track players (web)

### Config
- `AudioConfig` — root config (master, music, sfx, tracks, playlists, schemaVersion)
- `MusicConfig` — master, crossfade, contexts (dict), duckSources (dict), cueVariation
- `SfxConfig` — master, cueVariation
- `DuckSourceConfig` — ratio, attackSeconds, holdSeconds, releaseSeconds
- `TrackConfig` — build-only loudness/compress targets
- `AudioConfigBuilder` — fluent code-first config builder (core)
- `PlaylistConfig`, `PlaylistDefinition`, `PlaylistTrack` — playlist data model
- `PlaylistConfigBuilder` — fluent playlist authoring
- `AudioConfigJson` - JSON door in the core (tooling/persistence, not required at runtime)
- `PlaylistJson` — playlist wire format in the core

### Playlists
- `MusicPlaylistController` — backend-agnostic sequencing (Play, Next, Stop, CurrentTrack, TrackCompleted)
- `PlaylistRepeatMode` — Off, Playlist, Track
- `PlaylistShuffleMode` — Off, Bag (uses MarbleBag, never pure random)

### SFX variation
- `MarbleBag<T>` — draw-without-replacement randomizer (swap-remove, refill on empty, optional avoidImmediateRepeat, injectable Random)
- `CueVariation` / `CuePlay` — per-play pitch/volume jitter data and its resolved play
- `SfxManager` — cue-name one-shot playback with automatic music ducking and per-play cue jitter
- Music resolves its authored `MusicConfig.CueVariation` internally on every track start; SFX
  resolves `SfxConfig.CueVariation` per play. `MusicManager.NextCuePitch()` is private;
  `sfx.PlayNext()` draws a bag-selected SFX variant.

### Settings
- `PlayerAudioSettings`, `PlayerSettings` — player volume settings
- `IPlayerSettingsStore` — persistence interface
- `FilePlayerSettingsStore`, `WebPlayerSettingsStore`, `InMemoryPlayerSettingsStore` — store implementations
- `PlayerSettingsStoreSelector.Select(isBrowser, fileStore, browserStore)` — pure store selector

### Utilities
- `Gain` / `VolumeCurve` — perceptual slider/fader-to-amplitude mapping (quadratic, −60 dB floor at 0)
- `AudioLibrary` — two-directory convention (SFX/ + Tracks/), classification, candidate paths, manifest
- `AudioLibraryManifest` / `AudioLibraryManifestJson` — checked-in manifest for web hosts

### Two-directory audio convention
- Audio root holds `SFX/` (small one-shots, wav preferred) and `Tracks/` (loops/songs, ogg preferred)
- `AudioLibrary.ScanDirectory(root)` classifies live (desktop)
- `AudioLibrary.FromManifest(manifest)` for web (browsers can't enumerate directories)
- `duackcli scan <audioRoot>` writes `audio-manifest.json`
- Classification is case-SENSITIVE (`SFX`/`Tracks`, lowercase extensions only)

### Adapters

**Duack** (core, engine-agnostic):
- `SoundEffectMusicBackend` -- IMusicBackend over IAudioClipLibrary (overlap crossfade)
- `SoundEffectSfxBackend` -- ISfxBackend over IAudioClipLibrary
- `IAudioInstance` / `IAudioClip` / `IAudioClipLibrary` seams (headless testable)

**Duack.MonoGame** (desktop bindings):
- `SoundEffectClipLibrary` -- clip resolver with AudioLibrary candidate paths
- `SoundEffectAudioClip` / `SoundEffectAudioInstance` -- MonoGame SoundEffect bindings

**Duack.KNI** (web):
- `MediaPlayerMusicBackend` — IMusicBackend over platform MediaPlayer (HTML audio)
- `KniMediaPlayerHost` — production host over static MediaPlayer
- `WebAudioSfxBackend` — ISfxBackend over WebAudio API (one-shots overlap music)
- `LocalStorageKeyValueStorage` — browser localStorage settings storage

### Key APIs

```csharp
// Config (code-first)
var config = new AudioConfigBuilder()
    .WithMasterVolume(0.9f)
    .WithMusicMasterVolume(0.9f)
    .WithSfxMasterVolume(0.7f)
    .WithCrossfadeSeconds(1.5f)
    .WithContext("battle", 0.8f)
    .WithDuckSource("sfx", new DuckSourceConfig { Ratio = 0.25f, AttackSeconds = 0.06f, ReleaseSeconds = 0.35f })
    .Build();

// Music manager
var manager = new MusicManager(backend, playlistController, config.Music);
manager.Play("overworld");           // start playlist
manager.Pulse("sfx");               // duck music
manager.ReleaseDucks();             // immediately release all ducks
manager.IsDuckReady(0.99f);         // true when duck recovered
manager.Update(deltaSeconds);       // per-frame update


// SFX manager
var sfx = new SfxManager(library, backend, manager, sfxConfig: config.Sfx);
sfx.Play("hit", pitch: 0.25f);     // play cue + auto-duck + apply authored jitter
sfx.PlayNext();                     // draw from MarbleBag (no repeats)
sfx.Stop();                         // cut all one-shots

// MarbleBag
var bag = new MarbleBag<string>(cues, random, avoidImmediateRepeat: true);
var next = bag.Draw();              // draw without replacement
```

### SFX variation pattern
A cue may declare multiple variants plus per-play pitch/volume ranges. Pure data + pure math
in core; both directors resolve the authored `CueVariation` at play time (music via
`MusicConfig.CueVariation`, SFX via `SfxConfig.CueVariation`). Use `MarbleBag<T>` for variant
selection; `sfx.PlayNext()` draws one.

### Player settings
```csharp
PlayerSettings.Store = PlayerSettingsStoreSelector.Select(
    isBrowser: isBrowser,
    fileStore: new FilePlayerSettingsStore(),
    browserStore: new WebPlayerSettingsStore(storage));

var effective = PlayerSettings.ApplyOverrides(authoredConfig);  // layered read
// ... settings UI mutates effective ...
bool ok = PlayerSettings.SaveOverrides(effective);              // returns the store's verdict
```
- `VolumeCurve.ToAmplitude(slider)` — perceptual (quadratic, −60 dB floor at 0)
- `VolumeCurve.ToSlider(amplitude)` — exact inverse
- Volume properties are live: assigning `music.MasterVolume`, `sfx.MasterVolume`, or
  `sfx.SfxBusVolume` applies on the next frame or play — a settings slider needs no rebuild.

### Buses
Three default buses: **master**, **music**, **SFX**. The master bus scales everything (music and SFX alike) and is the top of the chain; music and SFX are the domain buses. Composition: `master bus × bus volume × context × duck × player settings`, master entering once at the top. Duck sources target the music bus, under the master.

### Studio & engineering terminology

Duack's design aligns with native professional audio engineering, DAW, digital console, and sampler concepts:

| Concept | Studio / engineering term | Notes |
| --- | --- | --- |
| duck source | sidechain compressor | Compressor with a sidechain input triggered by an external signal (e.g. dialogue or SFX) |
| attack, hold, release | attack, hold, release | Verbatim compressor envelope timing parameters |
| ratio | ratio | Verbatim compressor ratio ($n:1$) defining target gain reduction |
| context | scene, snapshot | Saved console mix states (Yamaha/X32 scene memory, Pro Tools mix snapshots); per-scene music volumes |
| master | master fader, 2-mix, main mix | Stereo master bus / top of the fader chain |
| music/SFX bus | group, aux, VCA, matrix | Console signal grouping; bus remains the umbrella term (bus master) |
| crossfade | crossfade, fade, ramp | Automation ramps and track transitions |
| fade in/out | fade up, fade down | Broadcast / studio verb forms for volume automation |
| playlist | playlist (broadcast / rotation) | Kept as playlist; radio rotations and broadcast sequences (DAWs use sessions) |
| cue | cue (production), cue mix (studio) | Production / theatrical sense of triggering sound assets (distinct from headphone cue mix) |
| one-shot | one-shot | Native sampler term for non-looping sample playback |
| MarbleBag variation | round-robin | Sample library articulation cycling / shuffled round-robin to eliminate the machine-gun effect |
| player settings | monitor level, monitor chain | The mix is the program; listener volume is the monitor level / chain |
| VolumeCurve | gain, fader law | Mixer fader response curve (audio-taper / logarithmic vs linear slider law) |
| attenuation | attenuation, gain reduction (GR) | Metered gain reduction applied by compressors and duckers |

### Crossfades
See the `crossfade` skill for detailed crossfade and scene transition patterns.

### Testing
- `tests/Duack.Tests/CodeOnly` — behavior, no serialization
- `tests/Duack.Tests/JsonRoundTrip` — player-settings wire format pins
- `tests/Duack.Tests/JsonRoundTrip` — config/playlist JSON wire format pins (core)
- `tests/Duack.Adapters.Tests` — adapter behavior via headless seams
- `tests/Duack.Sample.Tests` — sample StoryController + SampleAudio pins
- `tests/DuackCli.Tests` — CLI end-to-end

### CLI tools (`tools/DuackCli`)
- `duackcli convert <contentDir> <outDir>` (--playlists <file> | --all) — one of the two flags is required
- `duackcli scan <audioRoot>` — write audio-manifest.json for web hosts
- NAudio/OggVorbisEncoder live here only. Never referenced by src/ or samples/.

## Quickstart (install + wire)

Install:
```
dotnet add package Duack --prerelease
dotnet add package Duack.MonoGame --prerelease   # desktop
dotnet add package Duack.KNI --prerelease        # browser/WASM
```

Config (code-first):
```csharp
var config = new AudioConfigBuilder()
    .WithMusicMasterVolume(0.9f)
    .WithSfxMasterVolume(0.7f)
    .WithCrossfadeSeconds(1.5f)
    .WithContext("battle", 0.8f)
    .WithDuckSource("sfx", new DuckSourceConfig { Ratio = 0.25f, AttackSeconds = 0.06f, ReleaseSeconds = 0.35f })
    .Build();
```

Wire (desktop):
```csharp
var library = AudioLibrary.ScanDirectory("Content/Audio");
var playlists = library.BuildPlaylists();
var clips = new SoundEffectClipLibrary(library);
var backend = new SoundEffectMusicBackend(clips);
var controller = new MusicPlaylistController(playlists);
var manager = new MusicManager(backend, controller, config.Music);
```

Per-frame: `manager.Update(deltaSeconds)`; switch: `manager.ReleaseDucks(); manager.Play("battle");`; duck: `manager.Pulse("sfx")`.

See topic skills: ducking, playlists, sfx-variation, player-settings, audio-library, config, adapters, cli, crossfade.

