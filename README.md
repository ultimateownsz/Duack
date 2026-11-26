[![Discord](https://img.shields.io/discord/586997072373481494?logo=discord&style=social)](https://discord.gg/M2TvCx9zXa)

# Duack

Not duck, not quack. Duack.

An audio library that supports crossfades, ducking and playlists for MonoGame, KNI, and FlatRedBall 2 games — configured in code.

---

[![NuGet](https://img.shields.io/nuget/vpre/Duack?style=social&logo=nuget&label=Duack)](https://www.nuget.org/packages/Duack)

[![NuGet](https://img.shields.io/nuget/vpre/Duack.MonoGame?style=social&logo=nuget&label=Duack.MonoGame)](https://www.nuget.org/packages/Duack.MonoGame)

[![NuGet](https://img.shields.io/nuget/vpre/Duack.KNI?style=social&logo=nuget&label=Duack.KNI)](https://www.nuget.org/packages/Duack.KNI)

> [!IMPORTANT]
> Duack ships as a **preview** release: the API may change between versions.

## What is Duack

Duack is a simple audio library for MonoGame, KNI and FlatRedBall2 games, on desktop and in the browser. It handles the audio work every game eventually needs so you don't need to hand-roll it:


If the built-in path were enough, you would not be reading this. Duack is for the cases where you need:

| Features                   | Description                                                                                                                                                                                                    |
|----------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Crossfading                | Overlap two songs on desktop; on web, sequence fade-out → swap → fade-in. Also support standalone fade-in and fade-out.                                                                                        |
| Runtime playlist control   | Provide a `MusicPlaylistController` with `Play`, `Next`, and `Stop`, repeat modes, and bag-style shuffle so tracks don’t repeat until the bag empties.                                                         |
| Centralized volume chain   | Compose volume deterministically from Master → music/SFX buses, per-context scales, and player settings. User buses (ambience, UI...) add one more named factor, routed per cue.                              |
| User-defined buses         | Name buses in code (`AudioConfigBuilder.WithBus`) alongside the pre-registered master/music/sfx entries of one flat table (`AudioConfig.Buses`), route cues with a per-play `bus` argument, and control each at runtime with volume, mute, pause, a global master mute, and ref-counted duck holds, with a unified per-bus readback (`Views`). Buses are volume arithmetic — no mixer graph.  |
| Music [ducking](https://en.wikipedia.org/wiki/Ducking)              | Duck music under sound effects. Declare duck sources with ratio, attack, hold, and release; trigger them by cue, meter live gain, and support hot updates with `ReloadDuckSources()` if available.             |
| Cue variation              | Offer [XACT](https://en.wikipedia.org/wiki/Cross-platform_Audio_Creation_Tool)-style via the `MarbleBag` class for variant selection and authored per-play jitter: pitch for music, and pitch plus volume for SFX. |
| Persistent player settings | Support file, `localStorage`, and in-memory stores; sanitize settings and layer them over the authored mix.                                                                                                    |
| Shared desktop and web mix | Use one core over `IMusicBackend` and `ISfxBackend`; MonoGame and KNI adapters share configuration, playlists, and ducking.                                                                                    |
| Code-first authoring       | Provide an `AudioConfigBuilder`; use the `SFX/` and `Tracks/` directory convention to avoid cue registration.                                                                                                  |
| Self-sequencing playlists  | Let playlists sequence and shuffle tracks while preventing the same track from playing twice in a row.                                                                                                         |
| Lightweight dependencies   | Keep the core pure BCL, with heavyweight codec tooling outside the game.                                                                                                                                       |

**What it's not**: middleware.
- No editor, no mixer graph, no content pipeline beyond your audio files. Buses are volume arithmetic on one chain and effects (when they land) are baked at build time. The engine APIs play sounds; Duack owns the mix.
Duack replaces the ad-hoc audio code indie teams write themselves when middleware is too heavy.
- No UI tooling. For game UI, check out [Gum UI](https://github.com/vchelaru/Gum). Duack stays code-only, but transition state (crossfade progress, duck meter) is already queryable, and more can be exposed on request.

### Build tooling (optional, web/KNI)

For web builds, install the `duackcli` .NET tool to scan your audio directory into `audio-manifest.json`
and convert WAV masters to OGG:

```bash
dotnet tool install --global DuackCli
duackcli scan <audioRoot>
```

Install the DuackCli version matching your Duack package preview; the CLI
tags (`cli-v*`) release in lockstep with library previews.

### Samples

Each sample is a complete runnable game/tool built on the library — open the source to see real usage patterns.

| Sample | Description | Play                                                                          |
|--------|-------------|-------------------------------------------------------------------------------|
| [Duack.Sample.Desktop](samples/Duack.Sample.Desktop) | MonoGame DesktopGL auto-playing storyline (walk → notice → dialog → battle → loop, ~24–25 s, no controls): each cue rings on its own screen, every screen loads clean, live crossfade behind the battle sting, on-screen duck meter, `SfxManager` auto-duck, marble-bag hit cues, and one bus readback (`master`/`music`/`sfx`/`ambience`) line per scene in the console. | —                                                                             |
| [Duack.Sample.Concurrency](samples/Duack.Sample.Concurrency) | **Voice-limit demo** (MonoGame DesktopGL): clickable PLAY / MANY / STOP buttons fire one or fifty overlapping SFX cues against a cap of 8 voices with oldest-voice stealing; a two-digit counter and eight slot bars show the capped active-voice count, so the cap is visible without listening. | —                                                                             |
| [Duack.Sample.Web](samples/Duack.Sample.Web) | **Story demo** (Blazor WASM / KNI): the same auto-playing storyline with SFX, ducking, and crossfades, plus the per-scene bus readback line. Manifest-driven discovery, WebAudio one-shots. | [▶ Play in browser](https://ultimateownsz.github.io/Duack/)                   |
| [Duack.Sample.Crossfade](samples/Duack.Sample.Crossfade) | **Crossfade demo** (Blazor WASM / KNI): minimal showcase to A/B crossfade durations (4s/8s/12s/16s), toggle crossfade on/off, and hear hard cut vs equal-power ramp. | [▶ Play crossfade demo](https://ultimateownsz.github.io/Duack/crossfade-demo/) |

> [!IMPORTANT]
> The web demos need a click before sound plays — browsers suspend audio until a real user gesture on the screen.

## Documentation
Each topic is one self-contained Markdown guide in duack-skills/. Read them yourself, or copy the folder into .claude/skills/ so your AI coding assistant works with Duack's real API instead of guessing.

## Getting started

You need the [.NET 10 SDK.](https://dotnet.microsoft.com/download)

```sh
dotnet add package Duack --prerelease
dotnet add package Duack.MonoGame --prerelease   # desktop (Duack.KNI for browser/WASM)
```

Put your audio files in two folders; that's the whole setup. No cue registration, no content pipeline step (case-senstive: `SFX`/`Tracks`, lowercase extensions):
```
Content/Audio/
  SFX/       # one-shots (wav preferred)
  Tracks/    # loops and songs (ogg preferred)
```

Configure the mix in code:

```csharp
using Duack;
var config = new AudioConfigBuilder()
    .WithMasterVolume(0.9f)
    .WithCrossfadeSeconds(1.5f)
    .WithDuckSource("sfx", new DuckSourceConfig { Ratio = 0.25f })
    .Build();
```

Wire it in your library/engine (desktop, MonoGame):
```csharp
using Duack;
using Duack.MonoGame;

var library   = AudioLibrary.ScanDirectory(audioRoot);   // SFX/ + Tracks/ convention, no cue registration
var playlists = library.BuildPlaylists();    // one looping playlist per track, plus a bag-shuffled "music" playlist
var clips     = new SoundEffectClipLibrary(library);

var music = new MusicManager(
    new SoundEffectMusicBackend(clips),
    new MusicPlaylistController(playlists),
    config.Music, masterVolume: config.Buses[BusNames.Master].Volume);

var sfx = new SfxManager(library,
    new SoundEffectSfxBackend(clips), music,
    masterVolume: config.Buses[BusNames.Master].Volume,
    sfxBusVolume: config.Buses[BusNames.Sfx].Volume,
    sfxConfig: config.Sfx);

music.Play("overworld");          // playlist lookup: BuildPlaylists() named this single-track playlist after the track
sfx.Play("notice", duck: true);   // pulses the "sfx" duck source: the music dips and recovers automatically

// once per frame:
music.Update(dt);
```

Playlists for automatic sequencing (a battle sequence of intro -> loop -> victory, for example; `Repeat` applies to the whole playlist, so `Track` would loop the intro forever and never reach `battle-loop`):
```csharp
using Duack;

var playlists = new PlaylistConfigBuilder()
    .WithPlaylist("battle-sequence", p => p
        .Track("battle-intro", "Battle")
        .Track("battle-loop",  "Battle")
        .Track("victory",      "Battle")
        // Repeat is playlist-level: Off stops after "victory", Playlist replays the
        // sequence from the intro, Track loops whatever track is current — so to loop
        // "battle-loop" alone, give it its own single-track playlist with Repeat(Track).
        .Repeat(PlaylistRepeatMode.Off))
    .Build();
```

The same on the web (KNI / WASM); the browser can't list your files, so generate a manifest once and check it in:
```sh
dotnet tool install --global DuackCli
duackcli scan Content/Audio        # writes audio-manifest.json
```

```csharp
using Duack;
using Duack.KNI;
var manifest  = AudioLibraryManifestJson.Deserialize(manifestJson);
var library   = AudioLibrary.FromManifest(manifest);
var playlists = library.BuildPlaylists();    // same auto playlists as desktop

var music = new MusicManager(
    // contentRoot must point at Tracks/ — the backend default predates the two-directory convention and 404s under the standard layout
    new MediaPlayerMusicBackend(new KniMediaPlayerHost(), contentRoot: "Content/Audio/Tracks"),
    new MusicPlaylistController(playlists), config.Music, config.Buses[BusNames.Master].Volume);

// One-shot cues: WebAudio, URLs relative to the app root
var sfxUrls = library.Sfx.Select(cue => new KeyValuePair<string, string>(cue, $"Content/Audio/SFX/{cue}.wav"));
var sfx = new SfxManager(library, new WebAudioSfxBackend(new WebAudioNative(), sfxUrls), music,
    masterVolume: config.Buses[BusNames.Master].Volume,
    sfxBusVolume: config.Buses[BusNames.Sfx].Volume,
    sfxConfig: config.Sfx);
music.Update(dt);                 // same update code as desktop
```

## Need help?

- **Discord** (fastest): https://discord.gg/M2TvCx9zXa
- **Issues**: https://github.com/ultimateownsz/Duack/issues

#### Optional: the duackcli helper

> [!TIP]
> **Desktop-only tooling, not a runtime dependency.**
> The CLI runs on your dev machine at build
> time (encoding, scanning, normalizing). It never ships to the browser and is not required at
> runtime — the web head only reads the checked-in `audio-manifest.json` it produced.

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

Install the published tool and run it from anywhere:

```sh
dotnet tool install --global DuackCli
duackcli <command>
```

Working inside a clone of this repository, you can also run it in place:

```sh
dotnet run --project tools/DuackCli -- <command>
```
