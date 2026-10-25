---
name: adapters
description: Duack adapters — Duack.MonoGame (SoundEffectMusicBackend, SoundEffectSfxBackend, SoundEffectClipLibrary) and Duack.KNI (MediaPlayerMusicBackend, WebAudioSfxBackend, localStorage store). Use when wiring Duack to MonoGame/KNI, implementing IMusicBackend/ISfxBackend, or handling web autoplay and completion races.
---

# Duack — Adapters

Adapters are thin: no mixing logic. If logic is needed in two adapters, it belongs in core.

## IMusicBackend contract

```csharp
public interface IMusicBackend
{
    bool SupportsOverlap { get; }
    void PlayTrack(string trackName, float initialVolume, float pitch);
    void SetActiveVolume(float volume);
    void SetOutgoingVolume(float volume);
    void StopOutgoing();
    void Stop();
    bool IsTrackPlaying { get; }
    event EventHandler? TrackEnded;
    void Update(float deltaSeconds);
}
```

## Duack.MonoGame (desktop, overlap)

- `SoundEffectMusicBackend(IAudioClipLibrary clips)` — `SupportsOverlap = true`. Two tracks
  overlap during crossfade (outgoing + incoming equal-power). Plays tracks non-looped;
  looping is a playlist concern.
- `SoundEffectSfxBackend(IAudioClipLibrary)` — `ISfxBackend` over clip library: non-looping
  instance per cue, pitch/volume clamped to engine ranges, finished instances reaped on next
  play, `Dispose` stops everything.
- `SoundEffectClipLibrary` — clip resolver with `AudioLibrary` candidate paths; optional
  `AudioLibrary` so probes follow `CandidatePaths`; exact legacy flat order when omitted.
  `CandidateUris` helper pins both.
- `SoundEffectAudioClip` / `SoundEffectAudioInstance` / `IAudioClip` seams (headless testable).
- Settings store: `FilePlayerSettingsStore` (default).

Completion detection: poll `SoundEffectInstance.State` in per-frame update.

## Duack.KNI (web, sequenced)

- `MediaPlayerMusicBackend(IMediaPlayerHost host)` — `SupportsOverlap = false`. Single-track
  player: fade-out → swap → fade-in (total = CrossfadeSeconds). `MusicTransitionSequencer`.
- `KniMediaPlayerHost` — production host over static `MediaPlayer`.
- `WebAudioSfxBackend` — `ISfxBackend` over WebAudio API (one-shots overlap music).
- `LocalStorageKeyValueStorage` — browser localStorage settings storage.
- `IMediaPlayerHost` / `IWebAudioNative` seams (headless testable).

Completion detection: `OnEnded` event from HTML audio element, with the known race guard
(ended may fire before fade-in of next track is applied; desktop hard-stop + fade-in
semantics must not be assumed on web).

## Web-specific concerns

- Browser autoplay: `AudioContext`/`MediaPlayer` must resume from a real first gesture.
  Document in README; do not solve silently.
- Web sample uses manifest-driven music (no directory enumeration).
- `OperatingSystem.IsBrowser()` gates browser-only behavior; `AudioConfigJson.Save` is a
  no-op on browser (in the core).

## Wiring example

Desktop:
```csharp
var clips = new SoundEffectClipLibrary(content, library);
var musicBackend = new SoundEffectMusicBackend(clips);
var sfxBackend = new SoundEffectSfxBackend(clips);
var director = new MusicManager(musicBackend, controller, config.Music);
var sfx = new SfxManager(library, sfxBackend, director, sfxConfig: config.Sfx);
```

Web:
```csharp
var host = new KnMediaPlayerHost();
var musicBackend = new MediaPlayerMusicBackend(host);
var director = new MusicManager(musicBackend, controller, config.Music);
// no SFX backend on web (music-only); duck via director.Pulse
```

## Rules

- Adapters must be thin: no mixing logic.
- Logic needed in two adapters belongs in core.
- Adapters reference `src/Duack` + one engine framework with wide version range (e.g.
  `MonoGame.Framework.DesktopGL` `3.8.*`), never pinned.

See also: `audio-core`, `crossfade`, `audio-library`.
