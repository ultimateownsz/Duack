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

    public void SetBusVolume(string bus, float volume)
    {
        Calls.Add($"SetBusVolume({bus}, {volume})");
    }

    public void WarmHttpCache(string[] urls)
    {
        Calls.Add($"WarmHttpCache({string.Join(",", urls)})");
    }

    public void ReleaseAllBuffers()
    {
        Calls.Add("ReleaseAllBuffers");
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
    [Fact]
    public void SetBusVolume_forwards_the_resolved_bus_gain_through_the_seam()
    {
        // One gain node per bus in the playing chain: a bus volume change reaches the bridge so
        // the JS-side nodes - and with them the currently playing voices - move.
        var js = new FakeWebAudioNative();
        js.Buffers["hit.wav"] = true;
        var backend = new WebAudioSfxBackend(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

        backend.Play("hit", 0f, 1f); // a voice playing through the chain right now

        backend.SetBusVolume(BusNames.Sfx, 0.5f);
        backend.SetBusVolume(BusNames.Master, 0.25f);

        Assert.Contains("SetBusVolume(sfx, 0.5)", js.Calls);
        Assert.Contains("SetBusVolume(master, 0.25)", js.Calls);
    }

    [Fact]
    public void SetBusVolume_clamps_to_the_playable_range()
    {
        var js = new FakeWebAudioNative();
        var backend = new WebAudioSfxBackend(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

        backend.SetBusVolume(BusNames.Master, 5f);
        backend.SetBusVolume(BusNames.Sfx, -1f);

        Assert.Contains("SetBusVolume(master, 1)", js.Calls);
        Assert.Contains("SetBusVolume(sfx, 0)", js.Calls);
    }

    [Fact]
    public void SetBusVolume_after_dispose_is_a_noop()
    {
        var js = new FakeWebAudioNative();
        var backend = new WebAudioSfxBackend(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

        backend.Dispose();
        backend.SetBusVolume(BusNames.Sfx, 0.5f);

        Assert.DoesNotContain(js.Calls, c => c.StartsWith("SetBusVolume"));
    }

    [Fact]
    public void Dispose_releases_the_JS_side_prefetched_buffers()
    {
        var js = new FakeWebAudioNative();
        js.Buffers["hit.wav"] = true;
        var backend = new WebAudioSfxBackend(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

        backend.Dispose();

        Assert.Contains("ReleaseAllBuffers", js.Calls);
    }
    [Fact]
    public void WarmHttpCache_forwards_the_urls_exactly_and_in_ctor_order()
    {
        var js = new FakeWebAudioNative();
        var backend = new WebAudioSfxBackend(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

        backend.WarmHttpCache(new[] { "Content/Audio/Tracks/battle.ogg", "Content/Audio/Tracks/overworld.ogg" });

        Assert.Contains("WarmHttpCache(Content/Audio/Tracks/battle.ogg,Content/Audio/Tracks/overworld.ogg)", js.Calls);
    }

    [Fact]
    public void Construction_prefetches_exactly_the_ctor_urls()
    {
        // Pins what the ruling found already in place: every constructed cue URL is handed to
        // the bridge for fetch+decode in ctor order (one call, nothing missing, nothing extra).
        var js = new FakeWebAudioNative();
        var urls = new[]
        {
            new KeyValuePair<string, string>("hit", "Content/Audio/SFX/hit-1.wav"),
            new KeyValuePair<string, string>("notice", "Content/Audio/SFX/notice-video-game-sfx.wav"),
            new KeyValuePair<string, string>("battle-start", "Content/Audio/SFX/battle-start.wav"),
        };
        _ = new WebAudioSfxBackend(js, urls);

        Assert.Equal(
            "PreloadBuffers(Content/Audio/SFX/hit-1.wav,Content/Audio/SFX/notice-video-game-sfx.wav,Content/Audio/SFX/battle-start.wav)",
            js.Calls.Single(c => c.StartsWith("PreloadBuffers")));
    }
}