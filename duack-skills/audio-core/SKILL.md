---
name: audio-core
description: Duack audio library — core types, namespaces, quick reference. Use when working with Duack's audio mixing, ducking, crossfades, playlists, SFX variation, player settings, or the two-directory audio convention. Triggers on: Duack, DuckMixer, MusicManager, MusicVolumeMath, AudioConfig, SfxManager, MarbleBag, AudioLibrary, ISfxBackend, IMusicBackend, PlayerSettings.
---

# Duack — Audio Library for XNA-Family Engines

Duack is a config-driven audio mixer for MonoGame, KNI, FlatRedBall2, and FNA on desktop and
web. It adds what engine audio APIs don't ship: sidechain ducking, per-context music volumes,
crossfades, playlists, player-facing volume settings with persistence, and per-cue SFX
variation. Duack owns the mix and produces numbers; the user's engine owns playback.

**Status**: preview (0.x). API may change between minor versions.

## Hard architectural rules

- `src/Duack` is **pure BCL** (`System.Text.Json` only). Zero engine/XNA/MonoGame/KNI references.
  Never add a package or project reference to it.
- Backend glue lives in adapter packages: `Duack.MonoGame` (desktop) and `Duack.KNI` (web).
- **This preview is code-first.** The configuration surface is the C# API. The core also carries
  the JSON layer for tooling and persistence (`AudioConfigJson` / `PlaylistJson`); no
  `audio-config.json` ships with the library and none is required at runtime. Unknown sections
  are preserved via `[JsonExtensionData]`.
- Playlists have their own source-generated context (`Duack.PlaylistJson`), AOT-safe for WASM.
- Hot reload is **not built in**: hosts can re-parse config and call
  `MusicManager.ReloadDuckSources()` to swap duck params in place (duck state preserved);
  file watching is the host's responsibility.

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
- `CueVariation` / `CuePlay` — per-play pitch/volume jitter (`MusicManager.NextCuePitch()` /
  `SfxManager` per-play resolve)
- `SfxManager` — cue-name one-shot playback with automatic music ducking and per-play cue jitter

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

**Duack.MonoGame** (desktop):
- `SoundEffectMusicBackend` — IMusicBackend over SoundEffectInstance (overlap crossfade)
- `SoundEffectSfxBackend` — ISfxBackend over IAudioClipLibrary
- `SoundEffectClipLibrary` — clip resolver with AudioLibrary candidate paths

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
    .WithDuckSource("dialogue", new DuckSourceConfig { Ratio = 0.25f, AttackSeconds = 0.06f, ReleaseSeconds = 0.35f })
    .Build();

// Music director
var director = new MusicManager(backend, playlistController, config.Music);
director.Play("overworld");           // start playlist
director.Pulse("sfx");               // duck music
director.ReleaseDucks();             // immediately release all ducks
director.IsDuckReady(0.99f);         // true when duck recovered
director.Update(deltaSeconds);       // per-frame update

// SFX director
var sfx = new SfxManager(library, backend, director, sfxConfig: config.Sfx);
sfx.Play("hit", pitch: 0.25f);     // play cue + auto-duck + apply authored jitter
sfx.PlayNext();                     // draw from MarbleBag (no repeats)
sfx.Stop();                         // cut all one-shots

// MarbleBag
var bag = new MarbleBag<string>(cues, random, avoidImmediateRepeat: true);
var next = bag.Draw();              // draw without replacement
```

### SFX variation pattern
A cue may declare multiple variants plus per-play pitch/volume ranges. Pure data + pure math
in core; the music and SFX directors resolve the authored `CueVariation` at play time (music via
`MusicManager.NextCuePitch()`, SFX via its `SfxConfig.CueVariation`). Use `MarbleBag<T>` for variant
selection.

### Player settings
```csharp
PlayerSettings.Store = PlayerSettingsStoreSelector.Select(
    isBrowser: isBrowser,
    fileStore: new FilePlayerSettingsStore(),
    browserStore: new WebPlayerSettingsStore(storage));
var settings = PlayerSettings.Load();  // or PlayerSettings.Current
settings.MusicVolume = 0.8f;
settings.Save();
```
- `VolumeCurve.ToAmplitude(slider)` — perceptual (quadratic, −60 dB floor at 0)
- `VolumeCurve.ToSlider(amplitude)` — exact inverse

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
- `duackcli init <contentDir>` — scaffold audio-config.json from content scan
- `duackcli validate <config.json>` — schema + range validation
- `duackcli convert <input> <output>` — WAV to OGG for web
- `duackcli scan <audioRoot>` — write audio-manifest.json for web hosts
- NAudio/OggVorbisEncoder live here only. Never referenced by src/ or samples/.

## Quickstart (install + wire)

Install:
`
dotnet add package Duack
dotnet add package Duack.MonoGame   # desktop
# or Duack.KNI for web
`

Config (code-first):
`csharp
var config = new AudioConfigBuilder()
    .WithMusicMasterVolume(0.9f).WithSfxMasterVolume(0.7f)
    .WithCrossfadeSeconds(1.5f)
    .WithContext("battle", 0.8f)
    .WithDuckSource("sfx", new DuckSourceConfig { Ratio=0.25f, AttackSeconds=0.06f, ReleaseSeconds=0.35f })
    .Build();
`

Wire (desktop):
`csharp
var clips = new SoundEffectClipLibrary(content);
var backend = new SoundEffectMusicBackend(clips);
var controller = new MusicPlaylistController(playlists);
var director = new MusicManager(backend, controller, config.Music);
`

Per-frame: director.Update(deltaSeconds); Switch: director.ReleaseDucks(); director.Play("battle"); Duck: director.Pulse("sfx");.

See topic skills: ducking, playlists, sfx-variation, player-settings, audio-library, config, adapters, cli, crossfade.

