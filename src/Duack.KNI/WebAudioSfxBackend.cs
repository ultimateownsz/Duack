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
///
/// <para>
/// The JS side keeps one persistent gain node per bus of the playing chain: every play's
/// per-source gain is routed through the fixed <see cref="BusNames.Sfx"/> node into the
/// <see cref="BusNames.Master"/> node (then the destination), instead of an ad-hoc
/// per-source connection to the destination — the resolved chain the core composes into
/// the per-play volume is honoured node-for-node, and <see cref="SetBusVolume"/> moves the
/// persistent nodes so live bus volume changes reach currently playing voices. Known
/// limitation: the core's <see cref="ISfxBackend"/> seam hands the backend only the fully
/// composed per-play volume — it carries no bus name — so only the fixed chain nodes carry
/// audio today; per-play routing through user-bus nodes needs the core seam to pass the
/// resolved bus (a core contract change, out of this adapter's scope).
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

    /// <summary>
    /// Sets the live volume of the named bus's persistent gain node (clamped to [0, 1]), moving
    /// the currently playing voices through that node. The playing chain owns the fixed
    /// <see cref="BusNames.Sfx"/> and <see cref="BusNames.Master"/> nodes; other names are
    /// created as leaf nodes on demand for heads that drive them.
    /// </summary>
    public void SetBusVolume(string bus, float volume)
    {
        if (_disposed) return;
        try
        {
            _js.SetBusVolume(bus, Math.Clamp(volume, 0f, 1f));
        }
        catch
        {
            // Swallow: a live mixer update should never crash the frame.
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
