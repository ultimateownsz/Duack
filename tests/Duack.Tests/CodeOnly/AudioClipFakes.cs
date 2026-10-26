using System;
using System.Collections.Generic;
using Duack;

namespace Duack.Tests.CodeOnly;

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