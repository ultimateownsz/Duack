using System;
using System.Collections.Generic;
using Duack.KNI;
using Xunit;

namespace Duack.Adapters.Tests;

/// <summary>Fake <see cref="IWebAudioNative"/> for headless tests of <see cref="WebAudioSfxBackend"/>.</summary>
internal sealed class FakeWebAudioNative : IWebAudioNative
{
    public List<string> Calls { get; } = new();
    public Dictionary<string, bool> Buffers { get; } = new(StringComparer.Ordinal);

    public void InitAudioContext()
    {
        Calls.Add("InitAudioContext");
    }

    public void PreloadBuffers(string[] urls)
    {
        Calls.Add($"PreloadBuffers({string.Join(",", urls)})");
    }

    public bool HasBuffer(string url)
    {
        Calls.Add($"HasBuffer({url})");
        return Buffers.TryGetValue(url, out var loaded) && loaded;
    }

    public void PlaySource(string bufferUrl, float pitch, float volume)
    {
        Calls.Add($"PlaySource({bufferUrl}, pitch={pitch}, volume={volume})");
    }

    public void StopAll()
    {
        Calls.Add("StopAll");
    }
}

public class WebAudioSfxBackendTests
{
    [Fact]
    public void Play_UnloadedCue_ReturnsFalse()
    {
        var js = new FakeWebAudioNative();
        var backend = new WebAudioSfxBackend(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

        // Buffer hasn't loaded yet (fire-and-forget async), so Play returns false.
        Assert.False(backend.Play("hit", 0f, 1f));
    }

    [Fact]
    public void Play_LoadedCue_ReturnsTrueAndCallsPlaySource()
    {
        var js = new FakeWebAudioNative();
        js.Buffers["hit.wav"] = true; // buffer loaded
        var backend = new WebAudioSfxBackend(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

        var result = backend.Play("hit", 0f, 1f);

        Assert.True(result);
        Assert.Contains(js.Calls, c => c.StartsWith("PlaySource(hit.wav"));
    }

    [Fact]
    public void Play_UnknownCue_ReturnsFalse()
    {
        var js = new FakeWebAudioNative();
        var backend = new WebAudioSfxBackend(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

        Assert.False(backend.Play("missing", 0f, 1f));
    }

    [Fact]
    public void Stop_CallsStopAll()
    {
        var js = new FakeWebAudioNative();
        js.Buffers["hit.wav"] = true;
        var backend = new WebAudioSfxBackend(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

        backend.Play("hit", 0f, 1f);
        backend.Stop();

        Assert.Contains(js.Calls, c => c == "StopAll");
    }

    [Fact]
    public void Play_ClampsPitchToEngineRange()
    {
        var js = new FakeWebAudioNative();
        js.Buffers["hit.wav"] = true;
        var backend = new WebAudioSfxBackend(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

        backend.Play("hit", 5f, 1f); // pitch clamped to 1f
        backend.Play("hit", -5f, 1f); // pitch clamped to -1f

        Assert.Contains(js.Calls, c => c.Contains("pitch=1"));
        Assert.Contains(js.Calls, c => c.Contains("pitch=-1"));
    }

    [Fact]
    public void Play_ClampsVolumeToZeroOne()
    {
        var js = new FakeWebAudioNative();
        js.Buffers["hit.wav"] = true;
        var backend = new WebAudioSfxBackend(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

        backend.Play("hit", 0f, 5f); // volume clamped to 1f
        backend.Play("hit", 0f, -1f); // volume clamped to 0f

        Assert.Contains(js.Calls, c => c.Contains("volume=1"));
        Assert.Contains(js.Calls, c => c.Contains("volume=0"));
    }

    [Fact]
    public void Play_AfterDispose_ReturnsFalse()
    {
        var js = new FakeWebAudioNative();
        js.Buffers["hit.wav"] = true;
        var backend = new WebAudioSfxBackend(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

        backend.Dispose();

        Assert.False(backend.Play("hit", 0f, 1f));
    }
}
