# Duack

Duack is a config-driven audio mixer for XNA related libraries/engines (MonoGame, KNI, and FlatRedBall2) on desktop and in the browser. It adds what the engine audio APIs don't ship: sidechain ducking, per-context music volumes, crossfades, playlists, and player-facing volume settings that actually persist.

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

## Duack vs the built-in audio path

If you have ever written `MediaPlayer.Play` and then patched around it, this table is the pitch:

| You want | Built-in path | With Duack |
|----------|---------------|------------|
| Two songs crossfading | Not possible — `MediaPlayer` plays one song at a time | Crossfade on every track change: overlapped on desktop, sequenced fade → swap → fade on web, plus standalone fade-in/fade-out |
| A queue you can steer at runtime | The built-in queue is fixed once set; no track-completed contract | `MusicPlaylistController` with `Play`, `Next`, `Stop`, repeat modes, and bag-style shuffle — tracks don't repeat until the bag empties |
| One place for volume math | Per-song volume is unreliable after the first track | Master → music/SFX buses, per-context scales, and player settings composed in one deterministic chain |
| Music that ducks under sound effects | Not provided; every game hand-rolls it | Declare duck sources (ratio, attack, hold, release) and fire them by cue; live gain metering and `ReloadDuckSources()` for hot updates |
| XACT-style cue variation | XACT did not survive to modern XNA | Marble-bag variant selection plus per-play pitch/volume jitter for music and SFX |
| Player settings that persist | Your own file/localStorage code | File, localStorage, and in-memory stores, sanitized and layered over the authored mix |
| The same mix on desktop and web | Desktop and WASM audio paths diverge | One core over `IMusicBackend`/`ISfxBackend`; the MonoGame and KNI adapters share config, playlists, and ducking |
| A mix you author in code | Scattered constants and hand-registered cue names | Code-first `AudioConfigBuilder`; the two-directory convention (`SFX/`, `Tracks/`) removes cue registration |
| A dependency-light package | Wrappers can drag native codecs into the game | Core is pure BCL; the heavy codec tooling lives outside the game entirely |

If the built-in path were enough, you would not be reading this — the crossfade issues above are a decade old and structural.

## Quickstart

Configure the mix in code:

```csharp
using Duack;

var config = new AudioConfigBuilder()
    .WithMasterVolume(0.9f)
    .WithCrossfadeSeconds(1.5f)
    .WithDuckSource("notice", new DuckSourceConfig { Ratio = 0.25f, AttackSeconds = 0.06f })
    .Build();
```

Wire it to your engine and play (desktop, MonoGame):

```csharp
using Duack;
using Duack.MonoGame;

var library   = AudioLibrary.ScanDirectory(audioRoot);   // SFX/ + Tracks/ convention, no cue registration
var playlists = SampleAudio.BuildPlaylists(library);    // your playlist definitions
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

music.Play("overworld");
sfx.Play("notice", duck: true);   // the music dips and recovers automatically

// once per frame:
music.Update(dt);
```

The full loop — contexts, crossfades, duck metering, a scripted story — runs in `samples/Duack.Sample.Desktop`, and the same mix runs on the web in `samples/Duack.Sample.Web`. Both build and run from a clean clone.

This preview is **code-first**: the mix lives in C#, in your code, where it can be diffed, refactored, and version-controlled.

## Music contexts

Music contexts work like scenes in OBS: named audio states the game switches between. Each entry in the config is the context of a game state, for example the context of the battle state or the context of the overworld, each with its own volume scale.

## Buses

Duack ships three default buses: **master**, **music**, and **SFX**. The master bus scales everything (music and SFX alike) and is the top of the chain; music and SFX are the two domain buses. The volume composition is:

```
master bus x bus volume x context x duck x player settings
```

with the master entering once at the top. Duck sources target the music bus, under the master.

## Optional: the duackcli helper

Nothing in Duack requires the CLI. The library is fully usable from code alone. It exists for the moments automation is faster than typing:

- **Web builds without ceremony.** `convert` produces the OGG files a KNI/WASM project needs; SFX stay WAV unless asked. `scan` writes the AOT-safe audio manifest the web head loads instead of directory scanning.
- **Loudness you can measure.** `normalize` measures track loudness and writes gain-adjusted WAVs.

| Command | Syntax |
|---------|--------|
| `convert` | `duackcli convert <contentDir> <outDir> [--all]` |
| `scan` | `duackcli scan <audioRoot> [--out <file>]` |
| `normalize` | `duackcli normalize <dir> [<outDir>] [--out <dir>]` |

In this preview, run it from the repository: `dotnet run --project tools/DuackCli -- <command>`.

The same preview also ships markdown agent skills under `duack-skills/` (installable via the CLI, or by copying the directory into your agent folder, e.g. `.claude/skills/`).

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