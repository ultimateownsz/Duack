using System;
using System.Diagnostics;

namespace Duack.KNI;

/// <summary>
/// Web (KNI) <see cref="IMusicBackend"/>: rides the platform's native music stack, which on BlazorGL
/// is an HTML <c>&lt;audio&gt;</c> element - the browser itself fetches, decodes and streams the OGG.
/// No WASM PCM buffers.
///
/// <para>
/// The player cannot overlap two tracks, so <see cref="SupportsOverlap"/> is false and the core
/// <see cref="MusicManager"/> fades the old track out, swaps the single player, then fades the new
/// one in (see <c>MusicTransitionSequencer</c>). This adapter plays tracks NON-repeating; looping is a
/// playlist concern handled by the core, and a track reaching its natural end is reported through
/// <see cref="IMusicBackend.TrackEnded"/> (see <see cref="MediaEndState"/> for the stale-signal guard).
/// </para>
/// </summary>
public sealed class MediaPlayerMusicBackend : IMusicBackend
{
    private readonly IMediaPlayerHost _host;
    private readonly string _contentRoot;
    private readonly string _extension;
    private readonly MediaEndState _end;
    private bool _paused;

    /// <summary>Raised when the active track plays to its natural end.</summary>
    public event EventHandler? TrackEnded;

    /// <param name="host">The platform player seam.</param>
    /// <param name="contentRoot">Content-relative folder holding the tracks (default <c>Content/Audio</c>).</param>
    /// <param name="extension">Track file extension (default <c>.ogg</c>, the web format).</param>
    /// <param name="settleSeconds">Transition-churn window passed to <see cref="MediaEndState"/>.</param>
    public MediaPlayerMusicBackend(
        IMediaPlayerHost host,
        string contentRoot = "Content/Audio",
        string extension = ".ogg",
        float settleSeconds = 0.25f)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _contentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
        _extension = extension ?? throw new ArgumentNullException(nameof(extension));
        _end = new MediaEndState(settleSeconds);
        _host.StateChanged += OnHostStateChanged;
    }

    /// <inheritdoc />
    public bool SupportsOverlap => false;

    /// <inheritdoc />
    public bool IsTrackPlaying => _end.IsRunning && _host.IsPlaying;

    /// <inheritdoc />
    public bool IsPaused => _paused;

    /// <inheritdoc />
    public TimeSpan Position => TimeSpan.Zero;

    /// <inheritdoc />
    public TimeSpan Duration => TimeSpan.Zero;

    /// <inheritdoc />
    public void Seek(TimeSpan position) { }

    /// <inheritdoc />
    public void PlayTrack(string trackName, float initialVolume, float pitch)
    {
        // Pitch is not addressable on the platform player; the director's composition still applies.
        _host.IsRepeating = false;
        try
        {
            _host.Volume = initialVolume;
            _host.Play($"{_contentRoot}/{trackName}{_extension}");
            _end.Started();
        }
        catch (Exception ex)
        {
            // Missing/mid-copy file: log and stay silent rather than crash the frame. We do not
            // report a completion here, so the core does not spin through the whole playlist.
            Debug.WriteLine($"[Music] Could not play {_contentRoot}/{trackName}{_extension}: {ex.Message}");
            _end.StoppedByUs();
        }
    }

    /// <inheritdoc />
    public void SetActiveVolume(float volume) => _host.Volume = volume;

    /// <inheritdoc />
    public void SetOutgoingVolume(float volume)
    {
        // The platform player is single-track: there is no outgoing instance to fade.
    }

    /// <inheritdoc />
    public void StopOutgoing()
    {
        // No-op: a single player cannot hold an outgoing track.
    }

    /// <inheritdoc />
    public void Stop()
    {
        _end.StoppedByUs();
        _host.Stop();
        _paused = false;
    }

    /// <inheritdoc />
    public void Pause()
    {
        _host.Pause();
        _paused = true;
    }

    /// <inheritdoc />
    public void Resume()
    {
        _host.Resume();
        _paused = false;
    }

    /// <inheritdoc />
    public void Update(float deltaSeconds)
    {
        _end.Advance(deltaSeconds);

        // Safety net: if the platform never raised the state-change event, poll. The MediaEndState
        // latch guarantees the end is reported at most once across both paths.
        if (_end.IsRunning && _host.IsStopped && _end.ObserveStopped())
            TrackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void OnHostStateChanged(object? sender, EventArgs e)
    {
        if (_host.IsStopped && _end.ObserveStopped())
            TrackEnded?.Invoke(this, EventArgs.Empty);
    }
}