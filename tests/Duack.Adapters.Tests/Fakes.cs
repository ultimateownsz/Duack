using System;
using System.Collections.Generic;
using Duack.KNI;
using Duack.MonoGame;

namespace Duack.Adapters.Tests;

/// <summary>Fake <see cref="IAudioInstance"/> recording every call and letting tests flip playback.</summary>
internal sealed class FakeAudioInstance : IAudioInstance
{
    public bool IsLooped { get; set; }
    public float Volume { get; set; }
    public float Pitch { get; set; }
    public bool IsPlaying { get; private set; }
    public int PlayCount { get; private set; }
    public int StopCount { get; private set; }
    public bool Disposed { get; private set; }

    public void Play()
    {
        PlayCount++;
        IsPlaying = true;
    }

    public void Stop()
    {
        StopCount++;
        IsPlaying = false;
    }

    public void Dispose() => Disposed = true;

    /// <summary>Simulates the instance reaching the end of its (non-looped) playback.</summary>
    public void SimulateNaturalEnd() => IsPlaying = false;
}

/// <summary>Fake <see cref="IAudioClip"/> that hands out <see cref="FakeAudioInstance"/>s.</summary>
internal sealed class FakeAudioClip : IAudioClip
{
    public List<FakeAudioInstance> Instances { get; } = new();
    public bool Disposed { get; private set; }

    public IAudioInstance CreateInstance()
    {
        var instance = new FakeAudioInstance();
        Instances.Add(instance);
        return instance;
    }

    public void Dispose() => Disposed = true;
}

/// <summary>Fake <see cref="IAudioClipLibrary"/> mapping names to clips (missing names return null).</summary>
internal sealed class FakeAudioClipLibrary : IAudioClipLibrary
{
    private readonly Dictionary<string, FakeAudioClip> _clips = new(StringComparer.Ordinal);

    public int GetCount { get; private set; }

    public FakeAudioClip Add(string name)
    {
        var clip = new FakeAudioClip();
        _clips[name] = clip;
        return clip;
    }

    public IAudioClip? Get(string trackName)
    {
        GetCount++;
        return _clips.TryGetValue(trackName, out var clip) ? clip : null;
    }
}

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