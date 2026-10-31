using System;
using System.Collections.Generic;
using System.Linq;

namespace Duack;

/// <summary>
/// Backend-agnostic music playlist sequencer. It owns the sequencing decisions - which track is
/// current, when a completion advances the playlist, how repeat and shuffle behave - and reports the
/// result through <see cref="CurrentTrack"/> and <see cref="TrackCompleted"/>. The adapter owns
/// playback and completion detection: it calls <see cref="NotifyTrackCompleted"/> when the current
/// track ends, then starts whatever <see cref="CurrentTrack"/> names (applying the crossfade curve;
/// see <see cref="CrossfadeMath"/> and <see cref="MusicTransitionSequencer"/>).
///
/// <para>
/// The playlist is not built on the engine's queue (<c>MediaQueue</c>/<c>MediaPlayer</c>): that queue
/// cannot be modified at runtime and does not exist on the web backend. This controller is the single
/// sequencing path for both backends.
/// </para>
/// </summary>
public sealed class MusicPlaylistController
{
    private readonly PlaylistConfig _config;
    private readonly Random _random;

    private PlaylistDefinition? _definition;
    private List<PlaylistTrack> _tracks = new();
    private MarbleBag<int>? _bag;
    private int _bagRemaining;
    private int _index;
    private PlaylistTrack? _current;

    /// <param name="config">The playlists to sequence. Required.</param>
    /// <param name="random">Source of randomness for shuffle; defaults to <see cref="Random.Shared"/>.</param>
    public MusicPlaylistController(PlaylistConfig config, Random? random = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _random = random ?? Random.Shared;
    }

    /// <summary>True while a playlist is playing (and therefore <see cref="CurrentTrack"/> is non-null).</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>The name of the playlist started by the last successful <see cref="Play"/>, or null.</summary>
    public string? CurrentPlaylistName { get; private set; }

    /// <summary>The track the adapter should be playing, or null when stopped.</summary>
    public PlaylistTrack? CurrentTrack => _current;

    /// <summary>
    /// Raised once per completed track, carrying the track that just finished. Raised after the
    /// controller has advanced, so a handler reading <see cref="CurrentTrack"/> sees the successor
    /// (or null when the playlist stopped). Not raised by <see cref="Stop"/> (that is not a completion).
    /// </summary>
    public event Action<PlaylistTrack>? TrackCompleted;

    /// <summary>
    /// Starts <paramref name="playlistName"/> from the top. An unknown or empty playlist is ignored
    /// (the controller stops and stays idle rather than throwing), so a stray name cannot crash a game.
    /// </summary>
    public void Play(string playlistName) => Play(playlistName, 0);

    /// <summary>
    /// Starts <paramref name="playlistName"/> at <paramref name="startIndex"/> (clamped to a valid
    /// track), enabling resume from a previously captured position. An unknown/empty playlist is
    /// ignored. Shuffle bags are rebuilt and a random draw is used when shuffle is enabled.
    /// </summary>
    public void Play(string playlistName, int startIndex)
    {
        if (playlistName == null)
            throw new ArgumentNullException(nameof(playlistName));

        if (!_config.TryGet(playlistName, out var definition) || definition.Tracks.Count == 0)
        {
            Stop();
            return;
        }

        CurrentPlaylistName = playlistName;
        _definition = definition;
        _tracks = definition.Tracks;
        IsPlaying = true;
        _bag = null;
        _bagRemaining = 0;

        if (definition.Shuffle == PlaylistShuffleMode.Bag)
        {
            _bag = new MarbleBag<int>(Enumerable.Range(0, _tracks.Count), _random, avoidImmediateRepeat: true);
            _bagRemaining = _tracks.Count;
            _index = _bag.Draw();
            _bagRemaining--;
        }
        else
        {
            _index = Math.Clamp(startIndex, 0, _tracks.Count - 1);
        }

        _current = _tracks[_index];
    }

    /// <summary>
    /// Skips to the next track. Honours the playlist's repeat mode (wrapping a playlist loop, stopping
    /// at the end when repeat is off) and breaks a track-repeat loop, since a skip is an explicit
    /// request to move on. A no-op when not playing.
    /// </summary>
    public void Next()
    {
        if (!IsPlaying)
            return;

        Advance(manual: true);
    }

    /// <summary>
    /// Stops playback and clears the current state. Does not raise <see cref="TrackCompleted"/>.
    /// </summary>
    public void Stop()
    {
        IsPlaying = false;
        CurrentPlaylistName = null;
        _definition = null;
        _tracks = new List<PlaylistTrack>();
        _bag = null;
        _bagRemaining = 0;
        _index = 0;
        _current = null;
    }

    /// <summary>
    /// Reports that the adapter's current track finished playing. Advances the playlist per its
    /// repeat/shuffle rules and raises <see cref="TrackCompleted"/>. A no-op when not playing.
    /// </summary>
    public void NotifyTrackCompleted()
    {
        if (!IsPlaying)
            return;

        var finished = _current!;
        Advance(manual: false);
        TrackCompleted?.Invoke(finished);
    }

    private void Advance(bool manual)
    {
        if (_definition is null || _tracks.Count == 0)
        {
            Stop();
            return;
        }

        if (!manual && _definition.Repeat == PlaylistRepeatMode.Track)
            return;

        if (_definition.Shuffle == PlaylistShuffleMode.Bag)
        {
            if (_bagRemaining == 0)
            {
                if (_definition.Repeat == PlaylistRepeatMode.Off)
                {
                    Stop();
                    return;
                }

                _bagRemaining = _tracks.Count;
            }

            _index = _bag!.Draw();
            _bagRemaining--;
        }
        else
        {
            _index++;
            if (_index >= _tracks.Count)
            {
                if (_definition.Repeat == PlaylistRepeatMode.Off)
                {
                    Stop();
                    return;
                }

                _index = 0;
            }
        }

        _current = _tracks[_index];
    }
}
