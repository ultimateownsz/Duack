using System;
using System.Collections.Generic;
using Duack.KNI;
using Xunit;

namespace Duack.Adapters.Tests;

/// <summary>
/// Pins the web SFX backend's positional capability: a pan rides the interop call for panned
/// plays, a live voice re-mixes through <c>SetVoiceMix</c> with the same clamps every other
/// value reaches the bridge with, and stale/unknown/finished handles are silent no-ops.
/// </summary>
public class WebAudioSfxBackendPositionalTests
{
    private static WebAudioSfxBackend LoadedBackend(FakeWebAudioNative js) =>
        new(js, new[] { new KeyValuePair<string, string>("hit", "hit.wav") });

    [Fact]
    public void PlayTrackedAt_passes_pan_pitch_volume_clamped_to_the_bridge()
    {
        var js = new FakeWebAudioNative { Buffers = { ["hit.wav"] = true } };
        var backend = LoadedBackend(js);

        var handle = backend.PlayTrackedAt("hit", pan: 5f, pitch: 0.2f, volume: 0.9f);

        Assert.NotNull(handle);
        Assert.Contains(js.Calls, c => c.StartsWith("PlaySourceTrackedAt(hit.wav") &&
                                        c.Contains("pan=1") && // clamped to [-1, 1]
                                        c.Contains("pitch=0.2") &&
                                        c.Contains("volume=0.9"));
    }

    [Fact]
    public void PlayTrackedAt_follows_the_same_refusals_a_plain_play_has()
    {
        var js = new FakeWebAudioNative(); // nothing loaded
        var backend = LoadedBackend(js);

        Assert.Null(backend.PlayTrackedAt("hit", 0f)); // buffer not loaded
        Assert.Null(backend.PlayTrackedAt("missing", 0f)); // unknown cue
    }

    [Fact]
    public void SetVoiceMix_forwards_the_remix_clamped_to_the_bridge()
    {
        var js = new FakeWebAudioNative { Buffers = { ["hit.wav"] = true } };
        var backend = LoadedBackend(js);
        var handle = (long)backend.PlayTrackedAt("hit", pan: 0f)!;

        backend.SetVoiceMix(handle, volume: 3f, pan: -7f);

        Assert.Contains(js.Calls, c => c.StartsWith($"SetVoiceMix({handle}") &&
                                        c.Contains("volume=1") &&
                                        c.Contains("pan=-1"));
    }

    [Fact]
    public void SetVoiceMix_for_unknown_or_non_id_handles_does_not_reach_the_bridge()
    {
        var js = new FakeWebAudioNative { Buffers = { ["hit.wav"] = true } };
        var backend = LoadedBackend(js);

        backend.SetVoiceMix("not-an-id", volume: 1f, pan: 0f);
        backend.SetVoiceMix(-1L, volume: 1f, pan: 0f);
        backend.SetVoiceMix(0L, volume: 1f, pan: 0f);

        Assert.DoesNotContain(js.Calls, c => c.StartsWith("SetVoiceMix("));
    }

    [Fact]
    public void Positional_calls_after_dispose_are_no_ops()
    {
        var js = new FakeWebAudioNative { Buffers = { ["hit.wav"] = true } };
        var backend = LoadedBackend(js);
        backend.Dispose();

        Assert.Null(backend.PlayTrackedAt("hit", 0f));
        backend.SetVoiceMix(1L, volume: 1f, pan: 0f);

        Assert.DoesNotContain(js.Calls, c => c.StartsWith("PlaySourceTrackedAt("));
        Assert.Contains(js.Calls, c => c == "StopAll"); // Dispose's own teardown
    }
}
