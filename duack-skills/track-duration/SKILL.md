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
- **Desktop (MonoGame / `SoundEffectMusicBackend`)**: a `SoundEffectInstance` cannot seek.
  `SupportsSeek` is false; `Position` is the backend's own frame clock, `Duration` comes from the
  clip (`IAudioClip.DurationSeconds` — implement it in your clip library for a now-playing UI).
  `Seek` and a resume `Play(..., startPosition: ...)` are reported no-ops: the track restarts
  from its top. The track+index part of a resume still works on both backends.

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
