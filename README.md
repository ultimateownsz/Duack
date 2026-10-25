# Duack

Duack is a config-driven audio mixer for XNA-family engines (MonoGame, KNI, and FlatRedBall2) on desktop and in the browser. It adds what the engine audio APIs don't ship: sidechain ducking, per-context music volumes, crossfades, playlists, and player-facing volume settings that actually persist.

Not duck, not quack. Duack.

Duack ships as a **preview** release: the API may change between versions.

## Why Duack exists

In MonoGame and its family, crossfading two songs is architecturally impossible (`MediaPlayer` is a static, single-song API; requested since 2017: issues #5472, #6153, #6828), song volume is unreliable after the first track, the built-in queue can't be modified at runtime, and nothing provides ducking. XACT, the old Microsoft tool that handled variation and categories, is dead. Duack is the modern, fully managed replacement: no dead GUIs, no native runtimes, no ffmpeg.

Duack does not try to replace FMOD or Wwise — those platforms ship authoring editors, DSP, and profiling that Duack does not attempt. Duack replaces the ad-hoc audio code indie teams write themselves when middleware is too heavy.

## Quickstart

```csharp
var config = new AudioConfigBuilder()
    .WithMasterVolume(0.9f)
    .WithCrossfadeSeconds(1.5f)
    .WithDuckSource("dialogue", new DuckSourceConfig { Ratio = 0.25f, AttackSeconds = 0.06f })
    .Build();
```

This preview is **code-first**: build your mix in C#. An optional JSON file (`audio-config.json`)
exists as a secondary door via the separate `Duack.Config` package (provisional for this
preview) for tooling and file-based overlays; it is not required to use the library.

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

## Agent skills

Duack ships markdown agent skills under `duack-skills/`. To use them in your project:

- **Manual:** copy or submodule the `duack-skills/` directory into your agent folder
  (`.opencode/`, `.claude/skills/`, etc.).
- **CLI (optional):** `dotnet run --project tools/Duack.Tools -- init <contentDir> --skills`
  (use `--skills-dir <dir>` to choose the destination).

## Install

NuGet: `Duack` (core, code-first), `Duack.MonoGame`, `Duack.KNI`. `Duack.Config` and `Duack.Tools`
are provisional in this preview. (Published after the playlist milestone; see roadmap.)
