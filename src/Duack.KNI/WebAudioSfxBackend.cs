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
public sealed class WebAudioSfxBackend : ISfxBackend, ISfxVoiceControlBackend, ISfxPositionalBackend, IDisposable
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
    public bool Play(string cueName, float pitch, float volume) =>
        PlayTracked(cueName, pitch, volume) is not null;

    /// <inheritdoc />
    /// <remarks>Tracks the started source's JS-side id so <see cref="StopVoice"/> can stop it.</remarks>
    public object? PlayTracked(string cueName, float pitch = 0f, float volume = 1f)
    {
        if (_disposed) return null;
        if (!_cueUrls.TryGetValue(cueName, out var url)) return null;
        if (!_js.HasBuffer(url)) return null; // not loaded yet

        try
        {
            var pitchClamped = Math.Clamp(pitch, -1f, 1f);
            var volumeClamped = Math.Clamp(volume, 0f, 1f);
            var id = _js.PlaySourceTracked(url, pitchClamped, volumeClamped);
            return id != 0 ? id : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebAudioSfxBackend] Play failed for '{cueName}': {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public void StopVoice(object handle)
    {
        if (_disposed) return;
        if (handle is not long id || id <= 0) return;
        try
        {
            _js.StopSource(id);
        }
        catch
        {
            // Swallow: stopping should never crash the frame.
        }
    }

    /// <summary>
    /// Plays a tracked voice carrying a pan the core already computed: routed through a
    /// StereoPannerNode (constant-power — the desktop platform pan is a linear taper, the
    /// ADR-8-style acoustic difference the slice records) into the same sfx bus chain.
    /// Refusals match a plain play exactly: not loaded, unknown cue, or disposed means null.
    /// </summary>
    public object? PlayTrackedAt(string cueName, float pan, float pitch = 0f, float volume = 1f)
    {
        if (_disposed) return null;
        if (!_cueUrls.TryGetValue(cueName, out var url)) return null;
        if (!_js.HasBuffer(url)) return null;
        try
        {
            var id = _js.PlaySourceTrackedAt(url,
                Math.Clamp(pan, -1f, 1f),
                Math.Clamp(pitch, -1f, 1f),
                Math.Clamp(volume, 0f, 1f));
            return id != 0 ? id : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebAudioSfxBackend] Panned play failed for '{cueName}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Re-mixes one live voice (a moving source, a moving listener): volume re-ramped and pan
    /// moved on that voice's own nodes. Unknown, stale or invalid handles are silent no-ops.
    /// </summary>
    public void SetVoiceMix(object handle, float volume, float pan)
    {
        if (_disposed || handle is not long id || id <= 0) return;
        try
        {
            _js.SetVoiceMix(id,
                Math.Clamp(volume, 0f, 1f),
                Math.Clamp(pan, -1f, 1f));
        }
        catch
        {
            // Swallow: a live re-mix should never crash the frame.
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

    /// <summary>
    /// Warms the browser HTTP cache for the given URLs (fire-and-forget, nothing is decoded):
    /// the platform music player on BlazorGL fetches its tracks by URL on first play, so a demo
    /// warms the track URLs right after the start gesture and the first track plays from cache
    /// instead of the network. A missing URL logs loudly on the JS side and changes nothing
    /// here. No-op after <see cref="Dispose"/>.
    /// </summary>
    public void WarmHttpCache(string[] urls)
    {
        if (_disposed || urls is null || urls.Length == 0) return;
        try
        {
            _js.WarmHttpCache(urls);
        }
        catch
        {
            // Swallow: warming is opportunistic and never blocks startup.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _js.StopAll(); } catch { }
        try { _js.ReleaseAllBuffers(); } catch { } // the JS-side decoded buffers leave with the backend
        _cueUrls.Clear();
    }
}
