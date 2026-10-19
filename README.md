# Duack

Duack is a config-driven audio mixer for XNA-family engines (MonoGame, KNI, and FlatRedBall2) on desktop and in the browser. It adds what the engine audio APIs don't ship: sidechain ducking, per-context music volumes, crossfades, playlists, and player-facing volume settings that actually persist.

Not duck, not quack. Duack.

Duack ships as a **preview** release: the API may change between versions.

## Why Duack exists

In MonoGame and its family, crossfading two songs is architecturally impossible (`MediaPlayer` is a static, single-song API; requested since 2017: issues #5472, #6153, #6828), song volume is unreliable after the first track, the built-in queue can't be modified at runtime, and nothing provides ducking. XACT, the old Microsoft tool that handled variation and categories, is dead. Duack is the modern, fully managed replacement: no dead GUIs, no native runtimes, no ffmpeg.

## Quickstart

```csharp
var config = AudioConfigBuilder.Create()
    .Music(m => m.Master(0.9f).Crossfade(1.5f)
        .DuckSource("dialogue", d => d.Ratio(0.25f).Attack(0.06f)))
    .Build();
```

Or edit `audio-config.json` and hot-reload it while the game runs. Either door, same objects, same behavior.

## Music contexts

Music contexts work like scenes in OBS: named audio states the game switches between. Each entry in the config is the context of a game state, for example the context of the battle state or the context of the overworld, each with its own volume scale.

## What's included

- Sidechain ducking with attack / hold / release and hot reload
- Per-context music volumes and crossfades
- Playlists with bag-style shuffle (variation with a memory)
- Player settings: perceptual volume sliders, file / localStorage / in-memory persistence
- Per-cue SFX variation: marble-bag variants plus pitch ranges (the XACT-successor feature)
- Adapters for MonoGame (desktop) and KNI (browser/WASM)
- Offline tooling: config validation and WAV to OGG conversion, fully managed

## Scope

Duack owns the mix and produces the numbers; your engine plays the sound. Desktop (MonoGame DesktopGL) and web (KNI) are first-class. Mobile is not supported yet. No UI editor; the tooling package is the surface for building your own.

## Install

NuGet: `Duack` (core), `Duack.MonoGame`, `Duack.KNI`. (Published after the playlist milestone; see roadmap.)
