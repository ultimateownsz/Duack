---
name: track-duration
description: Optional consumer-side pattern for reporting track duration to Duack via MusicManager.CurrentTrackDurationSeconds, since Duack core does not probe audio files. Use when building a now-playing timer UI that needs elapsed/total time.
---

# Track Duration (consumer-side)

Duack core exposes playback position on `MusicManager`:

```csharp
public float CurrentTrackElapsedSeconds { get; }   // 0 when stopped
public float? CurrentTrackDurationSeconds { get; }  // null when unknown
```

Elapsed is provided by both backends. Duration is optional because Duack core is BCL-only and does not
read audio files. To show `mm:ss / mm:ss`, implement a resolver in the game:

```csharp
float? ResolveDuration(string trackName)
{
    // Probe Content/Audio/{Tracks,SFX}/...ogg/.wav, cache per cue.
    // OGG: NVorbis TotalSamples / SampleRate
    // WAV: RIFF dataBytes / byteRate
    return seconds > 0f ? seconds : null;
}
```

Feed `ResolveDuration(manager.CurrentTrack?.Name)` into your UI alongside `manager.CurrentTrackElapsedSeconds`.
Update only when the track changes or a transition begins (loop restart).
