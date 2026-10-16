using System;

namespace Duack;

/// <summary>
/// Core music director: owns playlist sequencing, crossfade realization, and per-frame gain
/// application. It is backend-agnostic — it talks to an <see cref="IMusicBackend"/> to play
/// tracks and apply gains, so both the MonoGame and KNI adapters stay thin.
///
/// <para>
/// Two crossfade models, both realized here so the adapters stay mechanical:
/// overlap-capable backends (desktop) demote the playing track to "outgoing" when
/// <see cref="IMusicBackend.PlayTrack"/> is called and both gains are driven with the
/// equal-power curve (<see cref="CrossfadeMath"/>); non-overlap backends (web, one
/// <c>MediaPlayer</c>) run <see cref="MusicTransitionSequencer"/> — fade the current track
/// out, switch, fade the new track in — and only ever touch the active gain.
/// </para>
///
/// <para>
/// Completion is adapter-reported: the backend fires <see cref="IMusicBackend.TrackEnded"/>
/// when the active track finishes and the director advances the playlist through
/// <see cref="MusicPlaylistController.NotifyTrackCompleted"/>.
/// </para>
/// </summary>
public class MusicDirector
{
    private readonly IMusicBackend _backend;
    private readonly MusicPlaylistController _playlist;
    private readonly MusicConfig _music;
    private readonly Random _random;
    private readonly MusicTransitionSequencer _sequencer = new();
    private bool _isCrossfading;
    private float _crossfadeElapsed;
    private string? _currentTrackName;

    /// <summary>Raised after the playlist advanced: carries the track that just completed.</summary>
    public event Action<PlaylistTrack>? TrackCompleted;

    /// <param name="backend">The platform backend that actually plays tracks.</param>
    /// <param name="playlist">The playlist sequencer to drive.</param>
    /// <param name="music">Music mix settings (crossfade duration, music cue variation). Optional.</param>
    /// <param name="random">Random source for cue variation; defaults to <see cref="Random.Shared"/>.</param>
    public MusicDirector(IMusicBackend backend, MusicPlaylistController playlist, MusicConfig? music = null, Random? random = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _playlist = playlist ?? throw new ArgumentNullException(nameof(playlist));
        _music = music ?? new MusicConfig();
        _random = random ?? Random.Shared;
        _playlist.TrackCompleted += track => TrackCompleted?.Invoke(track);
        _backend.TrackEnded += OnBackendTrackEnded;
    }

    /// <summary>True while a playlist is running or the backend is still producing sound.</summary>
    public bool IsPlaying => _playlist.IsPlaying || _backend.IsTrackPlaying;

    /// <summary>The track the playlist currently names (what the backend should be playing).</summary>
    public PlaylistTrack? CurrentTrack => _playlist.CurrentTrack;

    /// <summary>Starts <paramref name="playlistName"/> from the top.</summary>
    public void Play(string playlistName) => _playlist.Play(playlistName);

    /// <summary>Skips to the next track (honours the playlist's repeat mode).</summary>
    public void Next() => _playlist.Next();

    /// <summary>Stops playback and clears all transition state.</summary>
    public void Stop()
    {
        _playlist.Stop();
        _sequencer.Cancel();
        _isCrossfading = false;
        _crossfadeElapsed = 0f;
        _currentTrackName = null;
        _backend.Stop();
        _backend.StopOutgoing();
    }

    /// <summary>Advances playback by <paramref name="deltaSeconds"/>; call once per frame.</summary>
    public void Update(float deltaSeconds)
    {
        _backend.Update(deltaSeconds);

        if (!_playlist.IsPlaying)
        {
            if (_isCrossfading) StopTransition();
            return;
        }

        var next = _playlist.CurrentTrack;
        var nextName = next?.Name;
        if (!string.Equals(nextName, _currentTrackName, StringComparison.Ordinal))
        {
            BeginTransition(nextName);
        }

        if (!_isCrossfading)
        {
            _backend.SetActiveGain(1f);
            return;
        }

        var duration = _music.CrossfadeSeconds;
        if (_backend.SupportsOverlap)
        {
            UpdateOverlapCrossfade(deltaSeconds, duration);
        }
        else
        {
            UpdateSequencedCrossfade(deltaSeconds, duration);
        }
    }

    private void UpdateOverlapCrossfade(float deltaSeconds, float duration)
    {
        _crossfadeElapsed += deltaSeconds;
        var progress = duration <= 0f ? 1f : Math.Clamp(_crossfadeElapsed / duration, 0f, 1f);
        var (inGain, outGain) = CrossfadeMath.EqualPower(progress);
        _backend.SetActiveGain(inGain);
        _backend.SetOutgoingGain(outGain);
        if (progress >= 1f) FinishOverlapCrossfade();
    }

    private void UpdateSequencedCrossfade(float deltaSeconds, float duration)
    {
        var fadeOutCompleted = _sequencer.Advance(deltaSeconds, duration);
        if (fadeOutCompleted)
        {
            // The outgoing track has faded to silence: swap to the successor.
            _backend.Stop();
            var next = _playlist.CurrentTrack;
            var nextName = next?.Name;
            if (string.IsNullOrEmpty(nextName))
            {
                StopTransition();
                return;
            }

            _backend.PlayTrack(nextName, initialVolume: _sequencer.InGain, pitch: GetCuePitch(nextName));
            _currentTrackName = nextName;
        }

        // Fading out: the (old) active track follows OutGain. Fading in: it follows InGain.
        _backend.SetActiveGain(_sequencer.Phase == MusicFadePhase.FadingOut ? _sequencer.OutGain : _sequencer.InGain);
        if (_sequencer.Phase == MusicFadePhase.Steady) StopTransition();
    }

    private void BeginTransition(string? trackName)
    {
        if (string.IsNullOrEmpty(trackName))
        {
            StopTransition();
            return;
        }

        if (_backend.SupportsOverlap)
        {
            _backend.PlayTrack(trackName, initialVolume: 0f, pitch: GetCuePitch(trackName));
        }
        else if (_backend.IsTrackPlaying)
        {
            // Single-instance backend with something playing: fade it out first, switch later.
            _sequencer.BeginFadeOut();
            _currentTrackName = trackName;
            _isCrossfading = true;
            return;
        }
        else
        {
            _sequencer.BeginFadeIn();
            _backend.PlayTrack(trackName, initialVolume: _sequencer.InGain, pitch: GetCuePitch(trackName));
        }

        _currentTrackName = trackName;
        _isCrossfading = true;
        _crossfadeElapsed = 0f;
    }

    private void FinishOverlapCrossfade()
    {
        _backend.StopOutgoing();
        _backend.SetActiveGain(1f);
        _isCrossfading = false;
        _crossfadeElapsed = 0f;
    }

    private void StopTransition()
    {
        _sequencer.Cancel();
        _isCrossfading = false;
        _crossfadeElapsed = 0f;
        if (!_backend.IsTrackPlaying)
        {
            _currentTrackName = null;
        }
    }

    private void OnBackendTrackEnded(object? sender, EventArgs e)
    {
        var before = _playlist.CurrentTrack;
        _playlist.NotifyTrackCompleted();
        var after = _playlist.CurrentTrack;

        if (before != null && ReferenceEquals(before, after))
        {
            // Repeat=Track (or a single-track loop): the playlist keeps the same track, so no
            // Update-driven transition will fire — the director restarts playback itself.
            _backend.PlayTrack(after.Name, initialVolume: 0f, pitch: GetCuePitch(after.Name));
            _sequencer.BeginFadeIn();
            _isCrossfading = true;
            _crossfadeElapsed = 0f;
        }
    }

    private float GetCuePitch(string trackName)
    {
        return _music.CueVariation?.Resolve().Pitch ?? 0f;
    }
}