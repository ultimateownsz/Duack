# Duack

Duack is a configuration-driven audio mixer (config authored in C#) for XNA-related engines and libraries (MonoGame, KNI, and FlatRedBall2) on desktop and in the browser. It adds what the engine audio APIs don't ship: sidechain ducking, per-context music volumes, crossfades, playlists, and player-facing volume settings that actually persist.

Not duck, not quack. Duack.

Duack ships as a **preview** release: the API may change between versions.

## Why Duack exists

In MonoGame and similar libraries, crossfading two songs is architecturally
impossible (`MediaPlayer` is a static, single-song API; requested since 2017:
[#5472](https://github.com/MonoGame/MonoGame/issues/5472),
[#6153](https://github.com/MonoGame/MonoGame/issues/6153)), song volume is
unreliable after the first track, the built-in queue can't be modified at
runtime, and nothing provides ducking. The Song streaming path is still
shipping structural bugs nine years into those requests — shared
end-of-stream state that stops tracks early and breaks replacement and replay
([#9562](https://github.com/MonoGame/MonoGame/issues/9562), filed 2026).
XACT, the old Microsoft tool that handled variation and categories, is dead.
Duack is the modern, fully managed successor: no dead GUIs, no native runtimes,
no ffmpeg.

Duack does not try to replace FMOD or Wwise: those platforms ship authoring
editors, DSP, and profiling that Duack does not attempt. Duack replaces the
ad-hoc audio code indie teams write themselves when middleware is too heavy.

### Samples

Each sample is a complete runnable game built on the library — open the source to see real usage patterns.

| Sample | Description | Play                                                                          |
|--------|-------------|-------------------------------------------------------------------------------|
| [Duack.Sample.Desktop](samples/Duack.Sample.Desktop) | MonoGame DesktopGL scripted storyline (walk → notice → dialog → battle → loop): live crossfade, duck meter, `SfxManager` auto-duck, marble-bag hit cues. | —                                                                             |
| [Duack.Sample.Web](samples/Duack.Sample.Web) | **Story demo** (Blazor WASM / KNI): scripted storyline with SFX, ducking, and crossfades. Manifest-driven discovery, WebAudio one-shots. | [▶ Play in browser](https://ultimateownsz.github.io/Duack/)                   |
| [Duack.Sample.Crossfade](samples/Duack.Sample.Crossfade) | **Crossfade demo** (Blazor WASM / KNI): minimal showcase to A/B crossfade durations (4s/8s/12s/16s), toggle crossfade on/off, and hear hard cut vs equal-power ramp. | [▶ Play crossfade demo](https://ultimateownsz.github.io/Duack/crossfade-demo/) |

> The web demos need a click before sound plays — browsers suspend audio until a real user gesture ("Start").

## Duack vs the built-in audio path

If you have ever written `MediaPlayer.Play` and then patched around it, this table is the pitch:

| You want | Built-in path | With Duack |
|----------|---------------|------------|
| Two songs crossfading | Not possible — `MediaPlayer` plays one song at a time | Crossfade on every track change: overlapped on desktop, sequenced fade → swap → fade on web, plus standalone fade-in/fade-out |
| A queue you can steer at runtime | The built-in queue is fixed once set; no track-completed contract | `MusicPlaylistController` with `Play`, `Next`, `Stop`, repeat modes, and bag-style shuffle — tracks don't repeat until the bag empties |
| One place for volume math | Per-song volume is unreliable after the first track | Master → music/SFX buses, per-context scales, and player settings composed in one deterministic chain |
| Music that ducks under sound effects | Not provided; every game hand-rolls it | Declare duck sources (ratio, attack, hold, release) and fire them by cue; live gain metering and `ReloadDuckSources()` for hot updates |
| XACT-style cue variation | XACT did not survive to modern XNA | Marble-bag variant selection plus authored per-play jitter (pitch for music; pitch and volume for SFX) |
| Player settings that persist | Your own file/localStorage code | File, localStorage, and in-memory stores, sanitized and layered over the authored mix |
| The same mix on desktop and web | Desktop and WASM audio paths diverge | One core over `IMusicBackend`/`ISfxBackend`; the MonoGame and KNI adapters share config, playlists, and ducking |
| A mix you author in code | Scattered constants and hand-registered cue names | Code-first `AudioConfigBuilder`; the two-directory convention (`SFX/`, `Tracks/`) removes cue registration |
| A dependency-light package | Wrappers can drag native codecs into the game | Core is pure BCL; the heavy codec tooling lives outside the game entirely |

If the built-in path were enough, you would not be reading this — the crossfade issues above are a decade old and structural.

## Quickstart

Configure the mix in code (five lines):

```csharp
var config = new AudioConfigBuilder()
    .WithMasterVolume(0.9f)
    .WithCrossfadeSeconds(1.5f)
    .WithDuckSource("sfx", new DuckSourceConfig { Ratio = 0.25f })
    .Build();
```

Wire it to your engine and play (desktop, MonoGame):

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
sfx.Play("notice", duck: true);   // pulses the "sfx" duck source: the music dips and recovers automatically

// once per frame:
music.Update(dt);
```

The essential interaction is small: create a `MusicManager`, call `Play` for the playlist that
matches the new scene, and call `Next` to skip within the current playlist. Duack applies the
configured transition instead of requiring each game state to implement its own fade timers and
volume math.

On the web (KNI / Blazor WASM), the core stays identical - same config, same managers, same
playlists. Only the edges change: the library is built from the checked-in manifest instead
of a directory scan, and playback goes through the KNI adapters.

```csharp
using Duack;
using Duack.KNI;

var manifest = AudioLibraryManifestJson.Deserialize(manifestJson); // duackcli scan writes audio-manifest.json
var library = AudioLibrary.FromManifest(manifest);
var playlists = library.BuildPlaylists();    // same auto playlists as desktop

var music = new MusicManager(
    new MediaPlayerMusicBackend(new KniMediaPlayerHost(), contentRoot: "Content/Audio/Tracks"),
    new MusicPlaylistController(playlists),
    config.Music, masterVolume: config.MasterVolume);

// plus WebAudioSfxBackend for one-shot cues; music.Update(dt) once per frame as on desktop.
```

The full loop — contexts, crossfades, duck metering, a scripted story — runs in `samples/Duack.Sample.Desktop` (**Game demo**). The same mix runs on the web in `samples/Duack.Sample.Web` (**Game demo**) with a separate **[Crossfade demo](https://ultimateownsz.github.io/Duack/crossfade-demo/)** (`samples/Duack.Sample.Crossfade`) to isolate crossfade behavior. All build and run from a clean clone.

This preview is **code-first**: the mix lives in C#, in your code, where it can be diffed, refactored, and version-controlled. Ducking, crossfades, playlists, and per-context volumes are all **configuration-driven** — but the configuration is authored in code (via `AudioConfigBuilder` / `PlaylistConfigBuilder`).
## Music contexts

Music contexts work like scenes in OBS: named audio states the game switches between. Each entry in the config is the context of a game state, for example the context of the battle state or the context of an overworld, each with its own volume scale.

## Buses

Duack ships three default buses: **master**, **music**, and **SFX**. The master bus scales everything (music and SoundFX alike) and is the top of the chain; music and SFX are the two domain buses. The volume composition is:

```
master bus x bus volume x context x duck x player settings
```

with the master entering once at the top. Duck sources target the music bus, under the master.

## Optional: the duackcli helper

Nothing in Duack requires the CLI. The library is fully usable from code alone. It exists for the moments automation is faster than typing:

- **Files that won't load are reported, not swallowed.** `duackcli scan` warns (exit 0) about
  files the loader can never open — wrong type or an upper-cased extension — and the runtime
  loaders emit a `[Audio] Could not load ...` debug trace per failed cue (visible in Debug
  builds) instead of failing silently. Silent audio failures are the XNA pain Duack refuses to repeat.

- **Web builds without ceremony.** `convert` produces the OGG files a KNI/WASM project needs; SFX stay WAV unless asked. `scan` writes the AOT-safe audio manifest the web head loads instead of directory scanning.
- **Loudness you can measure.** `normalize` measures track loudness and writes gain-adjusted WAVs.

| Command | Syntax |
|---------|--------|
| `convert` | `duackcli convert <contentDir> <outDir> [--playlists <file> \| --all]` |
| `scan` | `duackcli scan <audioRoot> [--out <file>]` |
| `normalize` | `duackcli normalize <dir> [<outDir>] [--out <dir>]` |

In this preview, run it from the repository: `dotnet run --project tools/DuackCli -- <command>`.

> **Desktop-only tooling, not a runtime dependency.** The CLI runs on your dev machine at build
> time (encoding, scanning, normalizing). It never ships to the browser and is not required at
> runtime — the web head only reads the checked-in `audio-manifest.json` it produced.

The same preview also ships markdown agent skills under `duack-skills/` (copy the directory into your agent folder, e.g. `.claude/skills/`; the CLI does not install them yet).

## Scope

Duack owns the mix and produces the numbers; your engine plays the sound. Desktop (MonoGame DesktopGL) and web (KNI) are first-class. Mobile is not supported yet.

**No UI, ever.** Duack ships no editor or GUI of any kind — a deliberate boundary, not a limitation.

## Install

```
dotnet add package Duack --prerelease
dotnet add package Duack.MonoGame --prerelease   # desktop
dotnet add package Duack.KNI --prerelease        # browser/WASM
```

Targets .NET 10.
