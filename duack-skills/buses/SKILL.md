---
name: buses
description: Duack bus mixing — the flat bus table in AudioConfig.Buses with the pre-registered master/music/sfx entries and BusNames, authoring buses with AudioConfigBuilder.WithBus, routing cues per play, runtime volume/mute/pause/master mute on BusMixer, the unified Views readback (per-bus Volume, Muted, Paused, EffectiveGain, DuckRatio), and the no-silent-failure reports (BusFellBack, ZeroEffectiveGain). Use when wiring ambience/UI buses, mixing on the fixed music/SFX buses, or setting up per-bus volume and mute.
---

# Duack — Bus Mixing

## What a bus is

One entry in `AudioConfig.Buses` — a flat table of `BusConfig { Volume }` keyed by
lowercase, Ordinal-compared names. Three entries are pre-registered in every config:
`BusNames.Master`, `BusNames.Music`, `BusNames.Sfx`
(`"master"`, `"music"`, `"sfx"`) — table entries at the same level as your own buses,
not nested stages. Every play resolves through one chain:

master bus volume **x** bus volume **x** (muted or paused ? 0 : 1) **x** per-play
volume (**x** context scales for music) **x** the resolved bus's live duck ratio.
Buses are **volume arithmetic**, one more multiplication — not a mixer graph, and
there are no bus effects.

## Setup (authoring)

Define your own bus by calling `WithBus` with any lowercase name and a volume — that is
the whole step: no registration, no config file, no route table. From that point the name
is a mixing surface you can route plays to, hot-update, mute, pause, and read back exactly
like `music` and `sfx`.

```csharp
var config = new AudioConfigBuilder()
    .WithMasterVolume(0.8f)               // thin table write: Buses["master"].Volume
    .WithBus("ambience", 0.55f)           // any number of buses; volumes clamp to [0,1]
    .WithBus("ui", 0.9f)
    .Build();

config.Buses[BusNames.Sfx].Volume          // read the table directly — this is spelled
    = 0.75f;                               // Buses[name].Volume, there are no separate
                                           // MasterVolume fields on the config anymore

var sfx = new SfxManager(
    library, backend, musicManager,
    masterVolume: config.Buses[BusNames.Master].Volume,
    sfxBusVolume: config.Buses[BusNames.Sfx].Volume,
    sfxConfig: config.Sfx);       // the table rides in on the section configs
```

`WithMasterVolume`, `WithMusicMasterVolume`, `WithSfxMasterVolume`, and `WithBus`
keep their signatures and write the one table; there is no other storage. The three
standard buses always exist — even in a hand-composed `AudioConfig` — at full volume
unless the table says otherwise. The managers' constructor float parameters default
to 1 and win over the table at construction, so pass the table values in as above.

## Routing a cue (per-cue argument)

```csharp
sfx.Play("birds", bus: "ambience");  // routed to the bus
sfx.Play("click");                   // no bus = the "sfx" table entry
```

Routing is a per-play argument; it is never a context mapping (contexts stay music-only)
and never a registration step — any bus the mixer knows is immediately routable. An
un-routed cue always lands on the `sfx` bus.

## Runtime control (hot, no rebuild)

```csharp
sfx.Buses.SetBus("ambience", 0.7f);      // volume slider, [0,1], applies to new plays
sfx.Buses.SetMute("ambience", true);     // zero gain, flips independently of pause
sfx.Buses.SetPaused("gameplay", true);   // zero gain; distinct flag so a UI shows them apart
sfx.Buses.MasterMuted = true;            // global: every bus resolves to zero
sfx.Buses.VolumeOf("ambience");          // authored volume (mute/pause do not fold in)

sfx.Buses.Views;   // readback: per-bus Name, Volume, Muted, Paused,
                   // EffectiveGain AND DuckRatio — every bus, master/music/sfx included
sfx.Buses.Changed += () => ...;          // fires on any state change (frontend mixer hook)

musicManager.DuckRatioFor("ambience");   // the live duck ratio aimed at one target;
                                        // a delegating wrapper over the duck system
```

`Views` reads the duck system live at snapshot time, so `DuckRatio` shows the current
duck contribution per bus without embedding it in bus state.

## Ducking basics and holds

Duck sources are declared under `MusicConfig.DuckSources` as always — the music duck
under SFX is the settled case (see `../ducking/SKILL.md`).

### Ref-counted holds (overlapping sources)

```csharp
musicManager.BeginDuck("sfx-dip");  // each source holds once
musicManager.EndDuck("sfx-dip");    // releases only when the LAST hold ends
```

`BeginDuck`/`EndDuck` are ref-counted so a second source ending cannot drop the duck while
the first still plays. Over-releases are harmless; `ReleaseDucks()` clears everything
(scene switches).

## Bus-targeted ducks — supported, not yet validated

A duck source can name a bus as its `Target`, scaling plays routed to that bus instead of the
music (read the combined ratio with `MusicManager.DuckRatioFor(bus)`, or from `Views` as
`DuckRatio`). A duck source targeting `BusNames.Sfx` now scales the fixed un-routed path too —
one semantic, one path — which is an audible change for existing mixes (see the changelog's
preview-breaking note). The mechanism is implemented and unit-tested, but **no real project has
used an arbitrary bus target yet**, so this skill deliberately carries no worked example for
those: wire one only when a concrete need shows up, and record the use case where it came from.
Treat the setup as a candidate to validate by listening, not as a settled recipe.

`SfxManager` and `MusicManager` each hold their own `BusMixer` seeded from the same config
table, so per-bus mute/pause and the mixer's volumes are per-manager (snapshotted from the
table at construction) — a `SetBus` on one does not move the other. A game that hot-updates
should go through the manager's own surface (`sfx.Buses`, `musicManager.MasterVolume`) rather
than re-seeding a mixer by hand.

## No silent failures

- **Unknown bus name** → the play falls back to the SFX bus and `BusMixer.BusFellBack`
  fires with the bad name. Fix the typo it reports.
- **Zero effective gain** (muted bus, master mute, or a duck ratio of 0) →
  `SfxManager.ZeroEffectiveGain` fires with the cue name, for the fixed path exactly as for
  routed plays; the play makes no backend call and does not duck the music.

## Pitfalls and limits

- Bus volume applies **per play** (one-shots): muting mid-held note is heard on the next
  play, not the playing one. On the web backend,
  `WebAudioSfxBackend.SetBusVolume(bus, volume)` pushes a bus volume into a live gain node,
  so playing one-shots dip immediately there; the desktop path stays arithmetic-only.
- Per-bus active-voice counts need backend completion telemetry the `ISfxBackend` seam does
  not expose yet — `Views` deliberately omits them.
- Player settings stay the music/SFX slider pair; per-bus player volumes are deliberately
  not in the settings store.
- The fixed `master`/`music`/`sfx` buses are table entries with the same machinery as your
  buses — identical composition, fallback, and readback. A config authored with only the
  volume wrappers behaves exactly like one that writes the table directly.
