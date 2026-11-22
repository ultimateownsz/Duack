---
name: buses
description: Duack user-defined buses (slice C) — authoring buses with AudioConfigBuilder.WithBus, routing cues per play, runtime volume/mute/pause/master mute on BusMixer, ducking a bus, ref-counted duck holds, and the no-silent-failure reports (BusFellBack, ZeroEffectiveGain). Use when wiring ambience/UI/dialogue buses or setting up per-bus mixing.
---

# Duack — User-Defined Buses

## What a bus is

A named volume stage under the existing chain: Master → user bus → fixed SFX bus → per-play
volume (× per-context scales for music, × duck for the music or a bus). Buses are **volume
arithmetic**, one more multiplication — not a mixer graph, and there are no bus effects
(see `docs/plans/vnext/` for the edge of the roadmap).

## Setup (authoring)

```csharp
var config = new AudioConfigBuilder()
    .WithMasterVolume(0.8f)
    .WithBus("ambience", 0.55f)   // any number of buses; volumes clamp to [0,1]
    .WithBus("ui", 0.9f)
    .Build();

var sfx = new SfxManager(
    library, backend, musicManager,
    masterVolume: config.MasterVolume,
    sfxBusVolume: config.Sfx.MasterVolume,
    sfxConfig: config.Sfx);       // the buses ride in on the SfxConfig
```

## Routing a cue (per-cue argument)

```csharp
sfx.Play("birds", bus: "ambience");  // routed to the bus
sfx.Play("click");                   // no bus = the fixed SFX bus
```

Routing is a per-play argument (the default routing model, revisitable — see
`docs/plans/vnext/user-buses/STATUS.md`). Contexts stay music-only. An un-routed cue always
lands on the SFX bus.

## Runtime control (hot, no rebuild)

```csharp
sfx.Buses.SetBus("ambience", 0.7f);      // volume slider, [0,1], applies to new plays
sfx.Buses.SetMute("ambience", true);     // zero gain, flips independently of pause
sfx.Buses.SetPaused("gameplay", true);   // zero gain; distinct flag so a UI shows them apart
sfx.Buses.MasterMuted = true;            // global: every bus resolves to zero

sfx.Buses.Views;   // readback: per-bus Volume, Muted, Paused, EffectiveGain
sfx.Buses.Changed += () => ...;          // fires on any state change (frontend mixer hook)
```

## Ducking a bus

Duck sources gain a `Target`: the default `"music"` (as always) or a bus name.

```csharp
.WithDuckSource("dialogue", new DuckSourceConfig
{
    Ratio = 0.35f, AttackSeconds = 0.08f, ReleaseSeconds = 0.6f,
    Target = "ambience",   // the dialog ducks the ambience bus, not the music
})
```

While the hold is active, plays routed to `ambience` scale by the duck's current ratio.
Read a target's combined ratio with `MusicManager.DuckRatioFor("ambience")`.

### Ref-counted holds (overlapping speakers)

```csharp
musicManager.BeginDuck("dialogue");  // each speaker holds once
musicManager.EndDuck("dialogue");    // releases only when the LAST hold ends
```

`BeginDuck`/`EndDuck` are ref-counted so a second speaker ending cannot drop the duck while
the first still speaks. Over-releases are harmless; `ReleaseDucks()` clears everything
(scene switches).

## No silent failures

- **Unknown bus name** → the play falls back to the SFX bus and `BusMixer.BusFellBack`
  fires with the bad name. Fix the typo it reports.
- **Zero effective gain** (muted bus, master mute) → `SfxManager.ZeroEffectiveGain` fires
  with the cue name; the play makes no backend call and does not duck the music.

## Pitfalls and limits

- Bus volume applies **per play** (one-shots): muting mid-held note is heard on the next
  play, not the playing one. Music contexts and crossfades remain music-only.
- Per-bus active-voice counts need backend completion telemetry the `ISfxBackend` seam does
  not expose yet — `Views` deliberately omits them.
- Player settings stay the music/SFX slider pair; per-bus player volumes are deliberately
  not in the settings store.
- The fixed music/SFX buses compose regardless of buses; nothing about existing
  `AudioConfigBuilder` chains changes when no bus is authored.
