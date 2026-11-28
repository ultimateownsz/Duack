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
    /// <summary>How many frames the seek verify re-applies before giving up (reported).
    /// A cold src load can outlast a few hundred milliseconds, so the window is seconds, not
    /// frames at 60 fps.</summary>
    public const int MaxSeekVerifyFrames = 300;

    private readonly IMediaPlayerHost _host;
    private readonly string _contentRoot;
    private readonly string _extension;
    private readonly MediaEndState _end;
    private float _elapsed;
    private bool _paused;
    private TimeSpan? _pendingSeek;
    private int _seekVerifyFrames;

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
    public float CurrentTrackElapsedSeconds => IsTrackPlaying ? _elapsed : 0f;

    /// <inheritdoc />
    public float? CurrentTrackDurationSeconds => null;
    public bool IsPaused => _paused;

    /// <inheritdoc />
    public TimeSpan Position => IsTrackPlaying || _paused ? _host.Position : TimeSpan.Zero;

    /// <inheritdoc />
    public TimeSpan Duration => _host.Duration;

    /// <inheritdoc />
    public bool SupportsSeek => true;

    /// <inheritdoc />
    public void Seek(TimeSpan position)
    {
        // A seek fired at the swap lands while the element is still loading the new src, and
        // fastSeek is refused then. The first refusal is NOT terminal: the verify loop re-applies
        // on the following frames until the position sticks, or the frame bound gives up (reported).
        if (!_host.TrySeek(position))
        {
            Console.WriteLine($"[Duack] Seek to {position} not applied yet on the platform player; retrying on the following frames.");
        }

        _pendingSeek = position;
        _seekVerifyFrames = 0;
    }

    private void VerifySeekSettled()
    {
        if (_pendingSeek is not { } target) return;
        if (++_seekVerifyFrames > MaxSeekVerifyFrames)
        {
            Console.WriteLine($"[Duack] Seek to {target} did not stick within {MaxSeekVerifyFrames} frames; playback continues from {Position}.");
            _pendingSeek = null;
            return;
        }

        // The element plays while verifying, so the position drifts by the frame delta; a reset
        // by the src load snaps to zero - far outside any drift. Re-apply while the read position
        // is nowhere near the target; only a position that arrived (or a paused element, which
        // reads the target exactly) stops the verify.
        if (Math.Abs((Position - target).TotalSeconds) < 0.5)
        {
            _pendingSeek = null;
            return;
        }

        // A refused re-apply (the element still loading the new src) is retried on the next
        // frames; only the frame bound above ends the verify.
        _host.TrySeek(target);
    }

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
            _elapsed = 0f;
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
        _elapsed = 0f;
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
        if (IsTrackPlaying)
            _elapsed += deltaSeconds;
        _end.Advance(deltaSeconds);

        // Safety net: if the platform never raised the state-change event, poll. The MediaEndState
        // latch guarantees the end is reported at most once across both paths.
        VerifySeekSettled();

        if (_end.IsRunning && _host.IsStopped && _end.ObserveStopped())
            TrackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void OnHostStateChanged(object? sender, EventArgs e)
    {
        if (_host.IsStopped && _end.ObserveStopped())
            TrackEnded?.Invoke(this, EventArgs.Empty);
    }
}