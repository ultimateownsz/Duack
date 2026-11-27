---
name: track-duration
description: Optional consumer-side pattern for reporting track duration to Duack via MusicManager.CurrentTrackDurationSeconds, since Duack core does not probe audio files. Use when building a now-playing timer UI that needs elapsed/total time.
---

# Track Duration (consumer-side)

Duack core exposes playback position and duration on `MusicManager`:

```csharp
public TimeSpan Position { get; }     // zero when nothing is audible
public TimeSpan Duration { get; }     // zero when unknown
public bool SupportsSeek { get; }     // can Seek move the audible position on this backend?
public bool Seek(TimeSpan position);  // false = dropped, nothing changed
```

Legacy float readback (`CurrentTrackElapsedSeconds` / `CurrentTrackDurationSeconds?`) still exists;
prefer the TimeSpan surface.

## Backend capabilities

- **Web (KNI / `MediaPlayerMusicBackend`)**: `Position` and `Duration` read the platform player.
  `SupportsSeek` is true — `Seek` rides the browser `<audio>` element (`fastSeek`), so resume works.
- **Desktop (MonoGame / `SoundEffectMusicBackend`)**: a `SoundEffectInstance` cannot seek, but the
  clip's fully decoded buffer can — the desktop backend seeks by decode-from-offset: implement
  the `ISeekableAudioClip` capability in your clip library (`CreateInstance(TimeSpan)` decodes
  from the offset; `Release(instance)` frees the transient buffer) and `SupportsSeek` becomes
  true. Without the capability, `SupportsSeek` is false, `Position` is the backend's frame
  clock, `Duration` comes from `IAudioClip.DurationSeconds`, and `Seek`/a resume position are
  reported no-ops (the track restarts from its top; the track+index resume still works). The
  offset decode costs a decode-and-discard of the skipped portion for OGG — right for rare
  context-resume seeks, wrong for a scrub UI.

## Resuming a context

Capture `manager.Position` (plus playlist and track index) when leaving a context, then

```csharp
music.Play(playlistName, startIndex, startPosition: capturedPosition);
```

Check `music.SupportsSeek` first if an exact resume matters; without it you still get the right
track, just from its start.

## Now-playing UI without a seekable backend

To show `mm:ss / mm:ss` on desktop, implement `IAudioClip.DurationSeconds` in the game's clip
library (e.g. NVorbis `TotalSamples / SampleRate` for OGG, RIFF `dataBytes / byteRate` for WAV)
and read `manager.Position` / `manager.Duration` directly. Update only when the track changes or
a transition begins (loop restart).
