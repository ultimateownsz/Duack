using System;
using Duack.KNI;

namespace Duack.Adapters.Tests;

/// <summary>
/// Fake <see cref="IMediaPlayerHost"/> modelling a single-track platform player. Tests drive state
/// transitions explicitly so the backend's stale-signal guard can be exercised deterministically.
/// </summary>
internal sealed class FakeMediaPlayerHost : IMediaPlayerHost
{
    public event EventHandler<EventArgs>? StateChanged;

    public bool IsStopped => !IsPlaying;
    public bool IsPlaying { get; private set; }
    public float Volume { get; set; }
    public bool IsRepeating { get; set; }

    public string? LastUri { get; private set; }
    public int PlayCount { get; private set; }
    public int StopCount { get; private set; }
    public Exception? ThrowOnPlay { get; set; }

    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; }
    public int SeekCount { get; private set; }
    public TimeSpan? LastSeek { get; private set; }
    public bool SeekResult { get; set; } = true;

    /// <summary>Models the element's async src load: the next TrySeek "succeeds" but the load
    /// resets currentTime to zero (the browser's documented behavior for a seek before
    /// loadedmetadata); the seek is eaten, and only a re-apply after the load can stick.</summary>
    public bool ResetPositionOnNextSeek { get; set; }
    public int LoadResetCount { get; private set; }

    public bool TrySeek(TimeSpan position)
    {
        SeekCount++;
        LastSeek = position;
        if (ResetPositionOnNextSeek)
        {
            ResetPositionOnNextSeek = false;
            LoadResetCount++;
            Position = TimeSpan.Zero;
            return SeekResult;
        }
        if (SeekResult)
            Position = position;
        return SeekResult;
    }

    public void Play(string relativeUri)
    {
        if (ThrowOnPlay != null)
            throw ThrowOnPlay;
        LastUri = relativeUri;
        PlayCount++;
        IsPlaying = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        StopCount++;
        IsPlaying = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause() { }

    public void Resume() { }

    /// <summary>Simulates the platform finishing the current track (state -> stopped + event).</summary>
    public void SimulateNaturalEnd()
    {
        IsPlaying = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops playback WITHOUT raising <see cref="StateChanged"/>, modelling a missed event.</summary>
    public void StopWithoutEvent()
    {
        StopCount++;
        IsPlaying = false;
    }

    /// <summary>Raises a state change WITHOUT altering playback, to model a late/stale event.</summary>
    public void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}