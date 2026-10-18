using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the two-directory audio convention: an <see cref="AudioLibrary"/> discovers cue names from
/// a small-SFX directory (wav preferred) and a loops/tracks directory (ogg preferred) without any
/// manual name registration, reports where each cue lives via <see cref="AudioLibrary.CandidatePaths"/>,
/// and can auto-build the playlists a game switches between. Pure BCL, no engine.
/// </summary>
public class AudioLibraryTests : IDisposable
{
    private readonly List<string> _roots = new();

    public void Dispose()
    {
        foreach (var root in _roots)
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
        }
    }

    /// <summary>Creates a temp audio root containing the given (directory, file) pairs.</summary>
    private string CreateAudioRoot(params (string Dir, string File)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "duack-lib-" + Guid.NewGuid().ToString("N"));
        _roots.Add(root);
        Directory.CreateDirectory(root);
        foreach (var (dir, file) in files)
        {
            Directory.CreateDirectory(Path.Combine(root, dir));
            File.WriteAllText(Path.Combine(root, dir, file), string.Empty);
        }
        return root;
    }

    // ---- FromFiles: classification ----

    [Fact]
    public void FromFiles_splits_the_two_directories_by_cue_name()
    {
        var library = AudioLibrary.FromFiles(new[]
        {
            "SFX/hit.wav",
            "SFX/dialog.wav",
            "Tracks/overworld.ogg",
        });

        Assert.Equal(new[] { "dialog", "hit" }, library.Sfx);
        Assert.Equal(new[] { "overworld" }, library.Tracks);
    }

    [Fact]
    public void FromFiles_sorts_cue_names_ordinally_for_determinism()
    {
        var library = AudioLibrary.FromFiles(new[]
        {
            "SFX/zap.wav",
            "SFX/Alpha.wav",
            "Tracks/boom.ogg",
            "Tracks/Arena.ogg",
        });

        Assert.Equal(new[] { "Alpha", "zap" }, library.Sfx);
        Assert.Equal(new[] { "Arena", "boom" }, library.Tracks);
    }

    [Fact]
    public void FromFiles_dedupes_a_cue_shipped_in_several_extensions()
    {
        var library = AudioLibrary.FromFiles(new[] { "SFX/hit.wav", "SFX/hit.ogg" });

        Assert.Equal(new[] { "hit" }, library.Sfx);
    }

    [Fact]
    public void FromFiles_ignores_root_files_unknown_dirs_nested_paths_unsupported_types_and_dotfiles()
    {
        var library = AudioLibrary.FromFiles(new[]
        {
            "legacy.wav",            // audio root: not part of the convention
            "Music/jazz.ogg",        // unknown directory
            "SFX/nested/deep.wav",   // not a direct child of the convention directory
            "SFX/readme.txt",        // not a loadable audio type
            "Tracks/song.mp3",       // not a loadable audio type
            "SFX/.DS_Store",         // dotfile
        });

        Assert.Empty(library.Sfx);
        Assert.Empty(library.Tracks);
    }

    [Fact]
    public void FromFiles_requires_exactly_the_canonical_directory_and_extension_casing()
    {
        // Case-sensitive web hosts 404 on any mismatch, so the convention is strict: a lower-cased
        // directory or an upper-cased extension never classifies.
        var library = AudioLibrary.FromFiles(new[] { "sfx/hit.wav", "TRACKS/battle.ogg", "SFX/hit.WAV" });

        Assert.Empty(library.Sfx);
        Assert.Empty(library.Tracks);
    }

    [Fact]
    public void FromFiles_accepts_either_path_separator()
    {
        var library = AudioLibrary.FromFiles(new[] { @"SFX\hit.wav", "Tracks/battle.ogg" });

        Assert.Equal(new[] { "hit" }, library.Sfx);
        Assert.Equal(new[] { "battle" }, library.Tracks);
    }

    // ---- ScanDirectory ----

    [Fact]
    public void ScanDirectory_reads_both_directories_off_disk()
    {
        var root = CreateAudioRoot(
            ("SFX", "hit.wav"),
            ("SFX", "dialog.ogg"),
            ("SFX", "hit.ogg"),     // second extension of an existing cue
            ("Tracks", "overworld.ogg"),
            ("Tracks", "battle.wav"));

        var library = AudioLibrary.ScanDirectory(root);

        Assert.Equal(new[] { "dialog", "hit" }, library.Sfx);
        Assert.Equal(new[] { "battle", "overworld" }, library.Tracks);
    }

    [Fact]
    public void ScanDirectory_treats_a_missing_subdirectory_as_an_empty_side()
    {
        var root = CreateAudioRoot(("SFX", "hit.wav"));

        var library = AudioLibrary.ScanDirectory(root);

        Assert.Equal(new[] { "hit" }, library.Sfx);
        Assert.Empty(library.Tracks);
    }

    [Fact]
    public void ScanDirectory_throws_when_the_audio_root_does_not_exist()
    {
        var missing = Path.Combine(Path.GetTempPath(), "duack-missing-" + Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(() => AudioLibrary.ScanDirectory(missing));
    }

    [Fact]
    public void ScanDirectory_requires_the_canonical_directory_casing_on_disk()
    {
        // On Windows the filesystem hides the casing, but the loader candidate paths (and case-
        // sensitive hosts) do not: a lower-cased directory must not feed the convention.
        var root = CreateAudioRoot(("sfx", "hit.wav"));

        var library = AudioLibrary.ScanDirectory(root);

        Assert.Empty(library.Sfx);
    }

    // ---- CandidatePaths ----

    [Fact]
    public void Candidate_paths_prefer_the_owning_directory_then_the_legacy_flat_layout()
    {
        var library = AudioLibrary.FromFiles(new[] { "SFX/hit.wav", "Tracks/overworld.ogg" });

        Assert.Equal(
            new[] { "SFX/hit.wav", "SFX/hit.ogg", "hit.ogg", "hit.wav" },
            library.CandidatePaths("hit"));

        Assert.Equal(
            new[] { "Tracks/overworld.ogg", "Tracks/overworld.wav", "overworld.ogg", "overworld.wav" },
            library.CandidatePaths("overworld"));
    }

    [Fact]
    public void Candidate_paths_for_an_unknown_cue_probe_both_directories_then_the_flat_layout()
    {
        var library = AudioLibrary.FromFiles(new[] { "SFX/hit.wav" });

        Assert.Equal(
            new[] { "SFX/unknown.wav", "SFX/unknown.ogg", "Tracks/unknown.ogg", "Tracks/unknown.wav", "unknown.ogg", "unknown.wav" },
            library.CandidatePaths("unknown"));
    }

    [Fact]
    public void Candidate_paths_resolve_a_cue_in_both_directories_sfx_first()
    {
        var library = AudioLibrary.FromFiles(new[] { "SFX/dup.wav", "Tracks/dup.ogg" });

        Assert.Equal(
            new[] { "SFX/dup.wav", "SFX/dup.ogg", "dup.ogg", "dup.wav" },
            library.CandidatePaths("dup"));
    }

    [Fact]
    public void Candidate_paths_reject_a_null_or_blank_cue_name()
    {
        var library = AudioLibrary.FromFiles(new[] { "SFX/hit.wav" });

        Assert.Throws<ArgumentNullException>(() => library.CandidatePaths(null!));
        Assert.Throws<ArgumentException>(() => library.CandidatePaths(""));
    }

    [Fact]
    public void IsSfx_and_IsTrack_report_membership()
    {
        var library = AudioLibrary.FromFiles(new[] { "SFX/hit.wav", "Tracks/battle.ogg" });

        Assert.True(library.IsSfx("hit"));
        Assert.False(library.IsTrack("hit"));
        Assert.True(library.IsTrack("battle"));
        Assert.False(library.IsSfx("battle"));
        Assert.False(library.IsSfx("unknown"));
        Assert.False(library.IsTrack("unknown"));
    }

    // ---- ToPlaylist ----

    [Fact]
    public void ToPlaylist_wraps_every_discovered_track()
    {
        var library = AudioLibrary.FromFiles(new[] { "Tracks/battle.ogg", "Tracks/overworld.ogg" });

        var definition = library.ToPlaylist(context: "Music");

        Assert.Equal(new[] { "battle", "overworld" }, definition.Tracks.Select(t => t.Name));
        Assert.All(definition.Tracks, t => Assert.Equal("Music", t.Context));
        Assert.Equal(PlaylistRepeatMode.Playlist, definition.Repeat);
        Assert.Equal(PlaylistShuffleMode.Bag, definition.Shuffle);
    }

    // ---- BuildPlaylists ----

    [Fact]
    public void Build_playlists_creates_a_looping_single_track_playlist_per_track_plus_the_all_tracks_bag()
    {
        var library = AudioLibrary.FromFiles(new[] { "Tracks/battle.ogg", "Tracks/overworld.ogg" });

        var config = library.BuildPlaylists(new AudioLibraryPlaylistOptions
        {
            ContextForTrack = name => name == "battle" ? "Battle" : "Overworld",
        });

        Assert.True(config.TryGet("battle", out var battle));
        Assert.Equal(new[] { "battle" }, battle.Tracks.Select(t => t.Name));
        Assert.Equal("Battle", battle.Tracks[0].Context);
        Assert.Equal(PlaylistRepeatMode.Track, battle.Repeat);
        Assert.Equal(PlaylistShuffleMode.Off, battle.Shuffle);

        Assert.True(config.TryGet("music", out var all));
        Assert.Equal(new[] { "battle", "overworld" }, all.Tracks.Select(t => t.Name));
        Assert.Equal("Overworld", all.Tracks[1].Context);
        Assert.Equal(PlaylistRepeatMode.Playlist, all.Repeat);
        Assert.Equal(PlaylistShuffleMode.Bag, all.Shuffle);
    }

    [Fact]
    public void Build_playlists_defaults_every_context_to_empty()
    {
        var library = AudioLibrary.FromFiles(new[] { "Tracks/overworld.ogg" });

        var config = library.BuildPlaylists();

        Assert.True(config.TryGet("overworld", out var single));
        Assert.Equal(string.Empty, single.Tracks[0].Context);
    }

    [Fact]
    public void Build_playlists_of_an_empty_library_has_no_playlists()
    {
        var config = AudioLibrary.FromFiles(Array.Empty<string>()).BuildPlaylists();

        Assert.Empty(config.Playlists);
    }

    [Fact]
    public void Build_playlists_refuses_an_all_playlist_name_that_collides_with_a_track()
    {
        var library = AudioLibrary.FromFiles(new[] { "Tracks/music.ogg" });

        Assert.Throws<ArgumentException>(() =>
            library.BuildPlaylists(new AudioLibraryPlaylistOptions { AllTracksPlaylistName = "music" }));
    }

    [Fact]
    public void Build_playlists_can_skip_the_all_playlist_with_a_null_name()
    {
        var library = AudioLibrary.FromFiles(new[] { "Tracks/overworld.ogg" });

        var config = library.BuildPlaylists(new AudioLibraryPlaylistOptions { AllTracksPlaylistName = null });

        Assert.False(config.TryGet("music", out _));
        Assert.True(config.TryGet("overworld", out _));
    }
}
