using System;
using System.Collections.Generic;
using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Headless pin of the resume surface: <see cref="MusicManager.Play(string, int, TimeSpan?, float?)"/>
/// must start the requested track and apply the captured position ONLY on a backend that actually
/// seeks (<see cref="IMusicBackend.SupportsSeek"/>), reporting the dropped position otherwise, and
/// <see cref="MusicManager.Position"/>/<see cref="MusicManager.Duration"/>/<see cref="MusicManager.Seek"/>
/// forward to the backend verbatim.
/// </summary>
public class MusicPositionTests
{
    private sealed class FakeMusicBackend : IMusicBackend
    {
        public bool SupportsOverlap => true;
        public List<string> PlayedTracks { get; } = new();
        public List<TimeSpan> Seeks { get; } = new();
        public bool IsTrackPlaying { get; private set; }
        public float CurrentTrackElapsedSeconds { get; set; }
        public float? CurrentTrackDurationSeconds { get; set; }
        public bool IsPaused { get; private set; }
        public TimeSpan Position { get; set; } = TimeSpan.FromSeconds(7);
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(42);
        public bool SupportsSeek { get; set; }
        public event EventHandler? TrackEnded;

        public void PlayTrack(string trackName, float initialVolume, float pitch)
        {
            PlayedTracks.Add(trackName);
            IsTrackPlaying = true;
            CurrentTrackElapsedSeconds = 0f;
        }

        public void SetActiveVolume(float volume) { }
        public void SetOutgoingVolume(float volume) { }
        public void StopOutgoing() { }
        public void Stop()
        {
            IsTrackPlaying = false;
            IsPaused = false;
        }

        public void Seek(TimeSpan position) => Seeks.Add(position);
        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;
        public void Update(float deltaSeconds) { }
    }

    private static PlaylistConfig ThreeTracks() => new()
    {
        Playlists =
        {
            ["main"] = new PlaylistDefinition
            {
                Tracks =
                {
                    new PlaylistTrack { Name = "calm", Context = "Overworld" },
                    new PlaylistTrack { Name = "explore", Context = "Overworld" },
                    new PlaylistTrack { Name = "town", Context = "Overworld" },
                },
            },
        },
    };

    [Fact]
    public void Play_with_startIndex_starts_the_requested_track()
    {
        var backend = new FakeMusicBackend();
        var manager = new MusicManager(backend, new MusicPlaylistController(ThreeTracks(), new Random(1)));

        manager.Play("main", 2);

        Assert.Equal(new[] { "town" }, backend.PlayedTracks);
    }

    [Fact]
    public void Play_with_startPosition_seeks_a_seekable_backend()
    {
        var backend = new FakeMusicBackend { SupportsSeek = true };
        var manager = new MusicManager(backend, new MusicPlaylistController(ThreeTracks(), new Random(1)));

        manager.Play("main", 1, TimeSpan.FromSeconds(13));

        Assert.Equal(new[] { TimeSpan.FromSeconds(13) }, backend.Seeks);
        Assert.Equal(new[] { "explore" }, backend.PlayedTracks);
    }

    [Fact]
    public void Play_with_startPosition_never_seeks_an_unseekable_backend()
    {
        var backend = new FakeMusicBackend { SupportsSeek = false };
        var manager = new MusicManager(backend, new MusicPlaylistController(ThreeTracks(), new Random(1)));

        manager.Play("main", 1, TimeSpan.FromSeconds(13));

        Assert.Empty(backend.Seeks);
        Assert.Equal(new[] { "explore" }, backend.PlayedTracks);
    }

    [Fact]
    public void Play_without_a_startPosition_does_not_seek()
    {
        var backend = new FakeMusicBackend { SupportsSeek = true };
        var manager = new MusicManager(backend, new MusicPlaylistController(ThreeTracks(), new Random(1)));

        manager.Play("main");

        Assert.Empty(backend.Seeks);
    }

    [Fact]
    public void Position_and_Duration_forward_to_the_backend()
    {
        var backend = new FakeMusicBackend { Position = TimeSpan.FromSeconds(7), Duration = TimeSpan.FromSeconds(42) };
        var manager = new MusicManager(backend, new MusicPlaylistController(ThreeTracks(), new Random(1)));

        Assert.Equal(TimeSpan.FromSeconds(7), manager.Position);
        Assert.Equal(TimeSpan.FromSeconds(42), manager.Duration);
    }

    [Fact]
    public void Seek_returns_false_on_an_unseekable_backend_and_never_touches_it()
    {
        var backend = new FakeMusicBackend { SupportsSeek = false };
        var manager = new MusicManager(backend, new MusicPlaylistController(ThreeTracks()));

        Assert.False(manager.Seek(TimeSpan.FromSeconds(13)));
        Assert.Empty(backend.Seeks);
    }

    [Fact]
    public void Seek_on_a_seekable_backend_delegates_and_returns_true()
    {
        var backend = new FakeMusicBackend { SupportsSeek = true };
        var manager = new MusicManager(backend, new MusicPlaylistController(ThreeTracks()));

        Assert.True(manager.Seek(TimeSpan.FromSeconds(13)));
        Assert.Equal(new[] { TimeSpan.FromSeconds(13) }, backend.Seeks);
    }

    [Fact]
    public void SupportsSeek_forwards_to_the_backend()
    {
        var unseekable = new MusicManager(new FakeMusicBackend { SupportsSeek = false }, new MusicPlaylistController(ThreeTracks()));
        var seekable = new MusicManager(new FakeMusicBackend { SupportsSeek = true }, new MusicPlaylistController(ThreeTracks()));

        Assert.False(unseekable.SupportsSeek);
        Assert.True(seekable.SupportsSeek);
    }
}
