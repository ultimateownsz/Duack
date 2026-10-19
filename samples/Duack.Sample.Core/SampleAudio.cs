namespace Duack.Sample.Core;

/// <summary>
/// The demo configuration both samples share — this wiring used to be copy-pasted into the
/// desktop <c>Game1</c> and the web <c>Index.razor</c>. One definition here means the desktop
/// (MonoGame) and web (KNI) heads provably play the same mix, pinned by
/// <c>Duack.Sample.Tests</c> without any engine dependency.
/// </summary>
public static class SampleAudio
{
    /// <summary>Duck-source name the sfx cue path pulses (matches the "sfx" duck source below).</summary>
    public const string SfxDuckSource = "sfx";

    /// <summary>
    /// Builds the auto-playlists for a discovered library: one looping single-track playlist per
    /// track (scene music addressed by track name) plus the bag-shuffled <c>music</c> playlist.
    /// The <c>battle</c> track gets the <c>Battle</c> context; every other track gets
    /// <c>Overworld</c>.
    /// </summary>
    public static PlaylistConfig BuildPlaylists(AudioLibrary library) =>
        library.BuildPlaylists(new AudioLibraryPlaylistOptions
        {
            ContextForTrack = name => name == StoryController.BattlePlaylist ? "Battle" : "Overworld",
        });

    /// <summary>Builds the shared demo mix: masters, contexts, crossfade, ducking, playlists.</summary>
    public static AudioConfig BuildConfig(PlaylistConfig playlists) =>
        new AudioConfigBuilder()
            .WithMasterVolume(0.8f)
            .WithMusicMasterVolume(0.8f)
            .WithContext("Overworld", 1f)
            .WithContext("Battle", 0.9f)
            .WithDuckSource(SfxDuckSource, new DuckSourceConfig
            {
                Ratio = 0.3f,
                AttackSeconds = 0.06f,
                HoldSeconds = 0.15f,
                ReleaseSeconds = 0.1f,
            })
            .WithCrossfadeSeconds(1.2f)
            .WithPlaylists(playlists)
            .Build();
}
