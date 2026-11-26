---
name: spatial
description: 2D positional SFX in Duack - SetListener/PlayAt world positioning, saturating pan, distance models with an exact mute floor, SfxSpatialVoice re-mix for moving sources, the additive ISfxPositionalBackend seam, and WithSpatial config. Use when placing one-shot sounds in the world, panning gameplay audio, checking why a distant cue did not play, or wiring moving sources.
---

# Duack — 2D Positional SFX

Duack positions **one-shot SFX cues in the world**. Music is untouched: the
playlist engine plays in stereo as authored, and the mix tools for music stay crossfade and
ducking. Distances and offsets are plain floats — Duack never takes engine types (no Vector2,
no entities): engine-side wiring (camera, listeners on movers) is your code calling the
primitive inputs.

## Authoring the spatial settings

```csharp
var config = new AudioConfigBuilder()
    .WithSpatial(new SpatialConfig
    {
        ReferenceDistance = 1f,    // full volume at or inside this distance
        MaxDistance = 32f,         // the mute floor: at or beyond, the cue is not played
        Rolloff = 1f,              // strength of the fall-off between the two distances
        Model = DistanceModel.Inverse, // or Linear (straight-line fade)
        PanDistance = 12f,         // offset where the pan curve has closed most of its travel
        PanSaturation = 0.7f,      // pan ceiling: never hard +-1, so edges stay slightly centered
    })
    .Build();

var sfx = new SfxManager(library, backend, music, sfxConfig: config.Sfx);
```

The defaults are a working starting mix (`Inverse` model, saturation 0.7); they are revisitable,
not hand-validated — tune them on real content.

## The listener and positional plays

```csharp
sfx.SetListener(listenerX, listenerY);            // primitive input; anchor moves free
var (lx, ly) = sfx.ListenerPosition;              // readback

sfx.PlayAt("hit", sourceX, sourceY);              // positioned one-shot
var voice = sfx.PlayAt("hit", x, y, pitch, volume, bus: "world");
```

A positional play composes through the same chain as every other play: per-play volume, master
gain, the resolved bus gain (`bus:` routes through the same bus table as `Play`), the live duck
ratio, and then the distance gain — in that one path. Distance is the listener-relative
Euclidean distance; pan comes from the horizontal offset only (2D screen/left-right semantics).

## Moving sources

`PlayAt` returns an `SfxSpatialVoice`, or null when the play did not start (unknown cue,
backend refusal, silence). Call `SetPosition(x, y)` on the voice every time the source moves,
and the manager re-mixes that exact voice — the pan and gain it composed for the new position
reach the backend through the additive positional seam, keyed per voice. Move the listener with
`SetListener` and every live voice re-mixes around the new anchor.

```csharp
var voice = sfx.PlayAt("hit", x, y);
voice?.SetPosition(x + 4f, y);
voice?.Stop(); // silence now; slot bookkeeping still ages normally
```

Aging runs inside `Update(deltaSeconds)`: twice the cue's recorded lifetime after its start
(`VoiceLimits.DefaultVoiceSeconds` if no per-cue limit sets one, floored so the prune is at
least 0.1 s) the remix record is dropped, and a cue longer than that keeps sounding but stops
following its position.

## The mute floor releases, not mutes

A play at or past `MaxDistance` composes to zero total gain: it reports through
`ZeroEffectiveGain` and `LastPlayResult` stays `Played`, but nothing is played and **no voice
slot is consumed** — a very distant gameplay jam cannot crowd out important audio. A live voice
whose source walks past the floor is silenced (not stopped): remixes compose it to zero gain,
a `SetPosition` back inside the floor makes it audible again, and the limiter slot itself is
freed when the voice's recorded lifetime expires — silencing does not add or unlock slot time.

## Backend support and the pan difference

The seam is additive: a backend without it plays the composed volume mono-centered (same
api, no pan). The desktop `SoundEffectSfxBackend` and the web `WebAudioSfxBackend` both
implement it. One acoustic difference is recorded, not hidden: the web voice pans through a
`StereoPannerNode` (constant-power law); desktop pan is the platform `SoundEffectInstance.Pan`
(linear taper). The core hands both the same pan value; the two reproductions share the
endpoints (pan ±1 is full left on both) but differ at every off-center pan: the constant-power
law keeps left-squared plus right-squared constant, while the linear taper lets the combined
level rise between the extremes. Which law fits your game is a listening call on your target.

## Web (browser) wiring

The core of this page is backend-agnostic: `SetListener`, `PlayAt`, and `SfxSpatialVoice`
compose the same gain and pan for a given position on desktop and web — only the construction
differs, and how the platform renders that pan can differ (the note above). Browsers cannot
enumerate directories, so the web head builds the library from the checked-in manifest
(`duackcli scan`, see `audio-library`), preloads every cue through the WebAudio bridge, and
serves WAV by relative URL:

```csharp
var sfxUrls = library.Sfx.Select(cue =>
    new KeyValuePair<string, string>(cue, $"Content/Audio/SFX/{cue}.wav"));
var sfxBackend = new WebAudioSfxBackend(new WebAudioNative(), sfxUrls);

var sfx = new SfxManager(library, sfxBackend, music, "sfx", sfxConfig: config.Sfx);
sfx.SetListener(x, y);          // same seam as desktop from here on
var voice = sfx.PlayAt("hit", sourceX, sourceY);
voice?.SetPosition(x, y);       // re-mixes the live voice's StereoPannerNode + GainNode
```

Under the panner: one voice = one `AudioBufferSourceNode` → per-voice `GainNode` (the
composed volume, onset and remix ramps are 3 ms so nothing zippers), panned voices get a
`StereoPannerNode` inserted between them and the persistent sfx bus node — created at play
time for a voice that starts off-center, or mid-flight the first time a remix moves a
centered voice off center. Everything disconnects on natural end, `Stop()`, or stealing, so
a long session leaks no nodes.

Two browser facts matter: the `AudioContext` starts suspended until a user gesture, so
construct the backend (it resumes the context at init) inside the click that starts your
game — a page that builds it on load stays silent. And the world-unit scale is yours to
choose — pixels, meters, anything; the config's distances are in the same units your
`PlayAt` positions are.

See also: `adapters` (the full Duack.KNI wiring, autoplay policy), `buses` (routing a
positional cue to a user bus), `concurrency` (how a floor-muted play interacts with voice
caps — it never takes a slot).
