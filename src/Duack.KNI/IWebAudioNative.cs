using System;
using System.Runtime.InteropServices.JavaScript;

namespace Duack.KNI;

/// <summary>
/// Synchronous JS interop surface for the WebAudio SFX backend. Tests can substitute a fake
/// to exercise the backend headlessly; the production implementation uses
/// <see cref="JSImportAttribute"/> to call <c>window.DuackWebAudio.*</c> directly.
/// </summary>
public interface IWebAudioNative
{
    void InitAudioContext();
    void PreloadBuffers(string[] urls);
    bool HasBuffer(string url);
    void PlaySource(string bufferUrl, float pitch, float volume);
    void StopAll();

    /// <summary>
    /// Sets the volume of the named bus's persistent gain node in the playing chain (clamped to
    /// [0, 1] by the backend before it reaches here), so a live bus volume change moves the
    /// currently playing voices through that node. The chain owns the fixed
    /// <see cref="BusNames.Master"/>/<see cref="BusNames.Sfx"/> nodes by init; other names are
    /// created on demand (leaf nodes under the master node).
    /// </summary>
    void SetBusVolume(string bus, float volume);

    /// <summary>
    /// Warms the browser HTTP cache for the given URLs (fire-and-forget fetches, no audio
    /// decode): the platform music player on BlazorGL fetches its OGGs by URL on first play,
    /// so pre-populating the cache removes the first-track network delay. The player's
    /// one-time OGG decode on playback remains (the platform API offers no preload seam).
    /// </summary>
    void WarmHttpCache(string[] urls);

    /// <summary>
    /// Releases every prefetched/decoded AudioBuffer on the JS side so a disposed backend
    /// leaves no buffer memory behind (<see cref="WebAudioSfxBackend"/> calls it from
    /// <see cref="WebAudioSfxBackend.Dispose"/>).
    /// </summary>
    void ReleaseAllBuffers();
}

/// <summary>Production <see cref="IWebAudioNative"/> using synchronous [JSImport] calls.</summary>
public sealed partial class WebAudioNative : IWebAudioNative
{
    [JSImport("globalThis.DuackWebAudio.initAudioContext")]
    private static partial void InitAudioContextImpl();

    [JSImport("globalThis.DuackWebAudio.preloadBuffers")]
    private static partial void PreloadBuffersImpl(string[] urls);

    [JSImport("globalThis.DuackWebAudio.hasBuffer")]
    private static partial bool HasBufferImpl(string url);

    [JSImport("globalThis.DuackWebAudio.playSource")]
    private static partial void PlaySourceImpl(string bufferUrl, float pitch, float volume);

    [JSImport("globalThis.DuackWebAudio.stopAll")]
    private static partial void StopAllImpl();

    [JSImport("globalThis.DuackWebAudio.setBusVolume")]
    private static partial void SetBusVolumeImpl(string bus, float volume);

    [JSImport("globalThis.DuackWebAudio.warmHttpCache")]
    private static partial void WarmHttpCacheImpl(string[] urls);

    [JSImport("globalThis.DuackWebAudio.releaseAllBuffers")]
    private static partial void ReleaseAllBuffersImpl();

    public void InitAudioContext() => InitAudioContextImpl();
    public void PreloadBuffers(string[] urls) => PreloadBuffersImpl(urls);
    public bool HasBuffer(string url) => HasBufferImpl(url);
    public void PlaySource(string bufferUrl, float pitch, float volume) => PlaySourceImpl(bufferUrl, pitch, volume);
    public void StopAll() => StopAllImpl();
    public void SetBusVolume(string bus, float volume) => SetBusVolumeImpl(bus, volume);
    public void WarmHttpCache(string[] urls) => WarmHttpCacheImpl(urls);
    public void ReleaseAllBuffers() => ReleaseAllBuffersImpl();
}
