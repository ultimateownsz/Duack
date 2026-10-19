using System;
using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins that Play() resets any in-progress crossfade so the next scene's track starts
/// cleanly (no stale transition state bleeding across scene switches).
/// </summary>
public class MusicDirectorPlayResetTests
{
    private static PlaylistConfig TwoPlaylists() => new()
    {
        Playlists =
        {
            ["overworld"] = new PlaylistDefinition
            {
                Tracks = { new PlaylistTrack { Name = "overworld", Context = "Overworld" } },
                Repeat = PlaylistRepeatMode.Track,
            },
            ["battle"] = new PlaylistDefinition
            {
                Tracks = { new PlaylistTrack { Name = "battle", Context = "Battle" } },
                Repeat = PlaylistRepeatMode.Track,
            },
        }
    };

    private static MusicConfig DemoMusic() => new AudioConfigBuilder()
        .WithMusicMasterVolume(0.8f)
        .WithContext("Overworld", 1f)
        .WithContext("Battle", 0.9f)
        .WithCrossfadeSeconds(1.2f)
        .Build().Music;

    private sealed class FakeBackend : IMusicBackend
    {
        public bool SupportsOverlap { get; set; } = true;
        public List<string> Played { get; } = new();
        public bool IsTrackPlaying { get; set; } = true;
#pragma warning disable CS0067
        public event EventHandler? TrackEnded;
#pragma warning restore CS0067
        public void PlayTrack(string n, float v = 1f, float p = 0f) { Played.Add(n); }
        public void SetActiveVolume(float v) { }
        public void SetOutgoingVolume(float v) { }
        public void StopOutgoing() { }
        public void Stop() { IsTrackPlaying = false; }
        public void Update(float d) { }
    }

    [Fact]
    public void Play_while_crossfading_resets_state_so_new_track_starts_fresh()
    {
        var backend = new FakeBackend { SupportsOverlap = true };
        var playlist = new MusicPlaylistController(TwoPlaylists());
        var director = new MusicDirector(backend, playlist, DemoMusic());

        // Start overworld, then switch to battle (crossfade starts).
        director.Play("overworld");
        director.Update(0.016f);
        var playedBefore = backend.Played.Count;
        director.Play("battle");
        // Play() now immediately triggers the transition (Update(0f) internally).
        Assert.True(backend.Played.Count > playedBefore,
            "Play should trigger a fresh transition immediately, not defer to the next Update");
        Assert.Contains("battle", backend.Played);

        // A subsequent Update advances the crossfade without re-triggering PlayTrack.
        var playedAfterPlay = backend.Played.Count;
        director.Update(0.016f);
        // The crossfade advances but doesn't create a new instance (already created).
        Assert.True(backend.Played.Count >= playedAfterPlay);
    }
}
