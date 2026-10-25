# Duack

Duack is a config-driven audio mixer for XNA-family engines (MonoGame, KNI, and FlatRedBall2 on desktop and in the browser. It adds what the engine audio APIs don't ship: sidechain ducking, per-context music volumes, crossfades, playlists, and player-facing volume settings that actually persist.

Not duck, not quack. Duack.

Duack ships as a **preview** release: the API may change between versions.

## Why Duack exists

In MonoGame and similar libraries, crossfading two songs is architecturally impossible (`MediaPlayer` is a static, single-song API; requested since 2017: issues #5472, #6153, #6828), song volume is unreliable after the first track, the built-in queue can't be modified at runtime, and nothing provides ducking. XACT, the old Microsoft tool that handled variation and categories, is dead. Duack is the modern, fully managed replacement: no dead GUIs, no native runtimes, no ffmpeg.

Duack does not try to replace FMOD or Wwise — those platforms ship authoring editors, DSP, and profiling that Duack does not attempt. Duack replaces the ad-hoc audio code indie teams write themselves when middleware is too heavy.

## What you get over the built-in MonoGame/XNA audio path

Most MonoGame audio helpers are thin wrappers over `MediaPlayer` and `SoundEffect`, so they inherit
the same limitations. Duack sits above the engine backend and owns the mix:

| Need | Built-in / thin-wrapper path | Duack |
|------|------------------------------|-------|
| Crossfading two songs | `MediaPlayer` is a static, single-song API; you hand-roll `SoundEffectInstance` playback and volume ramps | Fade-in/fade-out and crossfade are part of the mixer. Overlap where the backend supports it (desktop); sequenced fade-out → swap → fade-in where it does not (KNI) |
| Playlists and queue | The built-in queue cannot be modified at runtime, and there is no track-completed/next/stop contract | `MusicPlaylistController` with `Next`, `Stop`, track-completed, repeat, and bag-style shuffle |
| Bus and volume model | Per-song volume is unreliable after the first track; mix math is your problem | Master → music/SFX buses, per-context scales, and player overrides composed in one place |
| Ducking | None; every game writes its own "lower the music under dialogue" hack | Sidechain-style duck sources with attack/hold/release, live gain-reduction metering, and `ReloadDuckSources()` without tearing down the manager |
| Cue variation | XACT's variation/category system is gone | Marble-bag variant selection plus per-play pitch/volume jitter for music and SFX |
| Player settings | DIY file/registry/localStorage code, usually without schema handling | File, localStorage, and in-memory stores; sanitized settings layered over the authored mix |
| Desktop and web parity | Desktop and Blazor/WASM paths usually diverge | One core over `IMusicBackend`/`ISfxBackend`; MonoGame and KNI adapters share the config and playlist model |
| Tooling | No first-party validation or content pipeline | `duackcli` validates, scans, normalizes, and converts WAV → OGG; AOT-safe manifest/config JSON for web |
| Dependency footprint | Wrappers can drag native runtimes or codec binaries into the game | Core is pure BCL; adapters reference the engine only. NAudio/OggVorbisEncoder live in the desktop CLI, never in `src/` or `samples/` |
| Authoring model | Scattered constants and hand-registered cue names | Code-first `AudioConfigBuilder`; the two-directory convention (`SFX/`, `Tracks/`) removes cue registration. Optional JSON is available for tooling and persistence |

What Duack deliberately does not do: it ships no editor or GUI, it is not FMOD/Wwise, it does not
play audio itself (your engine does), and mobile is not supported yet.

## Quickstart

```csharp
var config = new AudioConfigBuilder()
    .WithMasterVolume(0.9f)
    .WithCrossfadeSeconds(1.5f)
    .WithDuckSource("dialogue", new DuckSourceConfig { Ratio = 0.25f, AttackSeconds = 0.06f })
    .Build();
```

This preview is **code-first**: build your mix in C#. The core also carries a JSON layer
(`AudioConfigJson` / `PlaylistJson`) for tooling and file-based overlays, so an optional
`audio-config.json` can be read or written; no file is shipped with the library and none is
required to use it.

## Music contexts

Music contexts work like scenes in OBS: named audio states the game switches between. Each entry in the config is the context of a game state, for example the context of the battle state or the context of the overworld, each with its own volume scale.

## Buses

Duack ships three default buses: **master**, **music**, and **SFX**. The master bus scales everything (music and SFX alike) and is the top of the chain; music and SFX are the two domain buses. The volume composition is:

```
master bus x bus volume x context x duck x player settings
```

with the master entering once at the top. Duck sources target the music bus, under the master.

## What's included

- Sidechain ducking with attack / hold / release and hot reload
- Per-context music volumes and crossfades
- Playlists with bag-style shuffle (variation with a memory)
- Player settings: perceptual volume sliders, file / localStorage / in-memory persistence
- Per-cue SFX variation: marble-bag variants plus pitch ranges (the XACT-successor feature)
- Adapters for MonoGame (desktop) and KNI (browser/WASM)
- Offline tooling: config validation and WAV to OGG conversion, fully managed

## Scope

Duack owns the mix and produces the numbers; your engine plays the sound. Desktop (MonoGame DesktopGL) and web (KNI) are first-class. Mobile is not supported yet.

**No UI, ever.** Duack ships no editor or GUI of any kind — a deliberate extensibility boundary, not a limitation. The CLI is the first-party plugin surface: anyone building a visual tool, asset-pipeline integration, or debug overlay builds on the CLI's manifest and validation output.

## Commands

| Command | Syntax | Purpose |
|---------|--------|---------|
| `init` | `duackcli init <contentDir> [--out <file>] [--force] [--skills] [--skills-dir <dir>]` | Scaffold `audio-config.json` from a content directory scan; optionally install agent skills. |
| `validate` | `duackcli validate <config.json> [--playlists <file>]` | Schema + range validation with clear messages. |
| `convert` | `duackcli convert <contentDir> <outDir> [--all]` | WAV to OGG for web; SFX stay WAV unless `--all`. |
| `scan` | `duackcli scan <audioRoot> [--out <file>]` | Write `audio-manifest.json` from the two-directory layout. |
| `normalize` | `duackcli normalize <dir> [<outDir>] [--out <dir>] [--config <file>] [--write-config]` | Measure track loudness and write gain-normalized WAVs. |

## Agent skills

Duack ships markdown agent skills under `duack-skills/`. To use them in your project:

- **Manual:** copy or submodule the `duack-skills/` directory into your agent folder
  (`.opencode/`, `.claude/skills/`, etc.).
- **CLI (optional):** `dotnet run --project tools/DuackCli -- init <contentDir> --skills`
  (use `--skills-dir <dir>` to choose the destination).

## Install

NuGet: `Duack` (core, code-first), `Duack.MonoGame`, `Duack.KNI`. `DuackCli`
is provisional in this preview. (Published after the playlist milestone; see roadmap.)

