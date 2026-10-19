using System;
using System.Collections.Generic;
using System.Linq;

namespace Duack.KNI;

/// <summary>
/// Web (KNI) <see cref="ISfxBackend"/>: plays one-shot cues through the browser WebAudio API
/// (<c>AudioContext</c> + <c>AudioBufferSourceNode</c>), independent of MediaPlayer so SFX can
/// overlap the music track while <see cref="DuckMixer"/> dips the music volume.
///
/// <para>
/// All cue buffers are preloaded asynchronously at construction (fetch + <c>decodeAudioData</c>)
/// so <see cref="Play"/> stays synchronous: a cue not yet loaded reports false and never ducks,
/// matching the MonoGame backend's "missing clip = no duck" behaviour.
/// </para>
/// </summary>
public sealed class WebAudioSfxBackend : ISfxBackend, IDisposable
{
    private readonly IWebAudioNative _js;
    private readonly Dictionary<string, string> _cueUrls = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <param name="js">JS interop surface for WebAudio calls.</param>
    /// <param name="cueUrls">Cue name → relative URL map for every SFX cue to preload.</param>
    public WebAudioSfxBackend(IWebAudioNative js, IEnumerable<KeyValuePair<string, string>> cueUrls)
    {
        _js = js ?? throw new ArgumentNullException(nameof(js));

        foreach (var (cue, url) in cueUrls)
        {
            _cueUrls[cue] = url;
        }

        // Initialize AudioContext (requires user gesture; called after boot-screen click).
        _js.InitAudioContext();

        // Preload all cue buffers asynchronously (fire-and-forget). Play returns false for
        // cues whose buffer hasn't arrived yet, matching "missing clip = no duck".
        _js.PreloadBuffers(_cueUrls.Values.ToArray());
    }

    /// <inheritdoc />
    public bool Play(string cueName, float pitch, float volume)
    {
        if (_disposed) return false;
        if (!_cueUrls.TryGetValue(cueName, out var url)) return false;
        if (!_js.HasBuffer(url)) return false; // not loaded yet

        try
        {
            var pitchClamped = Math.Clamp(pitch, -1f, 1f);
            var volumeClamped = Math.Clamp(volume, 0f, 1f);
            _js.PlaySource(url, pitchClamped, volumeClamped);
            Console.WriteLine($"[WebAudioSfxBackend] Playing '{cueName}' at pitch={pitchClamped}, volume={volumeClamped}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebAudioSfxBackend] Play failed for '{cueName}': {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (_disposed) return;
        try
        {
            _js.StopAll();
        }
        catch
        {
            // Swallow: stopping should never crash the frame.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _js.StopAll(); } catch { }
        _cueUrls.Clear();
    }
}
