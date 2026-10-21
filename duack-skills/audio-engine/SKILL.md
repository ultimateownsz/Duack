---
name: audio-engine
description: Duack audio library — core types, namespaces, quick reference. Use when working with Duack's audio mixing, ducking, crossfades, playlists, SFX variation, player settings, or the two-directory audio convention. Triggers on: Duack, DuckMixer, MusicDirector, MusicVolumeMath, AudioConfig, SfxDirector, MarbleBag, AudioLibrary, ISfxBackend, IMusicBackend, PlayerSettings.
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
- **This preview is code-only.** The JSON wire format lives in the separate **provisional**
  package `Duack.Config` (`AudioConfigJson` / `PlaylistJson`). The core has no JSON API;
  unknown sections are preserved via `[JsonExtensionData]`.
- Playlists have their own source-generated context (`Duack.PlaylistJson`), AOT-safe for WASM.
- Hot reload is **not built in**: hosts can re-parse config and call
  `MusicDirector.ReloadDuckSources()` to swap duck params in place (duck state preserved);
  file watching is the host's responsibility.

## Namespace migration

All types live in `Duack` (core), `Duack.MonoGame` (desktop adapter), `Duack.KNI` (web adapter).
Every namespace → `Duack`.

## Core types quick reference

### Mixing
- `DuckMixer` — sidechain ducking with attack/hold/release per named source
- `MusicVolumeMath` — effective volume math (master × context × duck × fade)
- `CrossfadeMath` — equal-power crossfade curve
- `MusicDirector` — owns playlist sequencing, crossfade realization, ducking, context scales
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
- `AudioConfigJson` - JSON door in **Duack.Config** (provisional, not shipped in this preview)
- `PlaylistJson` — playlist wire format in **Duack.Config**

### Playlists
- `MusicPlaylistController` — backend-agnostic sequencing (Play, Next, Stop, CurrentTrack, TrackCompleted)
- `PlaylistRepeatMode` — Off, Playlist, Track
- `PlaylistShuffleMode` — Off, Bag (uses MarbleBag, never pure random)

### SFX variation
- `MarbleBag<T>` — draw-without-replacement randomizer (swap-remove, refill on empty, optional avoidImmediateRepeat, injectable Random)
- `CueVariation` / `CuePlay` — per-play pitch/volume jitter
- `SfxDirector` — cue-name one-shot playback with automatic music ducking

### Settings
- `PlayerAudioSettings`, `PlayerSettings` — player volume settings
- `IPlayerSettingsStore` — persistence interface
- `FilePlayerSettingsStore`, `WebPlayerSettingsStore`, `InMemoryPlayerSettingsStore` — store implementations
- `PlayerSettingsStoreSelector.Select(isBrowser, fileStore, browserStore)` — pure store selector

### Utilities
- `VolumeCurve` — perceptual slider-to-amplitude mapping (quadratic, −60 dB floor at 0)
- `AudioLibrary` — two-directory convention (SFX/ + Tracks/), classification, candidate paths, manifest
- `AudioLibraryManifest` / `AudioLibraryManifestJson` — checked-in manifest for web hosts

### Two-directory audio convention
- Audio root holds `SFX/` (small one-shots, wav preferred) and `Tracks/` (loops/songs, ogg preferred)
- `AudioLibrary.ScanDirectory(root)` classifies live (desktop)
- `AudioLibrary.FromManifest(manifest)` for web (browsers can't enumerate directories)
- `duack scan <audioRoot>` writes `audio-manifest.json`
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
var director = new MusicDirector(backend, playlistController, config.Music);
director.Play("overworld");           // start playlist
director.Pulse("sfx");               // duck music
director.ReleaseDucks();             // immediately release all ducks
director.IsDuckReady(0.99f);         // true when duck recovered
director.Update(deltaSeconds);       // per-frame update

// SFX director
var sfx = new SfxDirector(library, backend, director);
sfx.Play("hit", pitch: 0.25f);     // play cue + auto-duck
sfx.PlayNext();                     // draw from MarbleBag (no repeats)
sfx.Stop();                         // cut all one-shots

// MarbleBag
var bag = new MarbleBag<string>(cues, random, avoidImmediateRepeat: true);
var next = bag.Draw();              // draw without replacement
```

### SFX variation pattern
A cue may declare multiple variants plus per-play pitch/volume ranges. Pure data + pure math
in core; adapters apply pitch at play time. Use `MarbleBag<T>` for variant selection.

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

### Crossfades
See the `crossfade` skill for detailed crossfade and scene transition patterns.

### Testing
- `tests/Duack.Tests/CodeOnly` — behavior, no serialization
- `tests/Duack.Tests/JsonRoundTrip` — player-settings wire format pins
- `tests/Duack.Config.Tests` — config/playlist JSON wire format pins (Duack.Config)
- `tests/Duack.Adapters.Tests` — adapter behavior via headless seams
- `tests/Duack.Sample.Tests` — sample StoryController + SampleAudio pins
- `tests/Duack.Tools.Tests` — CLI end-to-end

### CLI tools (`tools/Duack.Tools`)
- `duack init <contentDir>` — scaffold audio-config.json from content scan
- `duack validate <config.json>` — schema + range validation
- `duack convert <input> <output>` — WAV to OGG for web
- `duack scan <audioRoot>` — write audio-manifest.json for web hosts
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
var director = new MusicDirector(backend, controller, config.Music);
`

Per-frame: director.Update(deltaSeconds); Switch: director.ReleaseDucks(); director.Play("battle"); Duck: director.Pulse("sfx");.

See topic skills: ducking, playlists, sfx-variation, player-settings, audio-library, config, adapters, cli, crossfade.
