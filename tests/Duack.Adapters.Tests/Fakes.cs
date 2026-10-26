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