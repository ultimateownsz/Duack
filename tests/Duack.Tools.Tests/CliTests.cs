namespace DuackCli.Tests;

/// <summary>
/// End-to-end tests for the <c>duackcli</c> CLI, driven through <see cref="Cli.Run"/> with captured
/// writers and a temp content directory. Each verb's exit code and filesystem effects are pinned.
/// </summary>
public class CliTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "duack-cli-" + Guid.NewGuid().ToString("N"));

    public CliTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private (int code, string outText, string errText) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Cli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private string Path2(params string[] parts) => Path.Combine(new[] { _dir }.Concat(parts).ToArray());

    // ---- general ----

    [Fact]
    public void No_arguments_prints_usage_and_fails()
    {
        var (code, _, err) = Run();
        Assert.NotEqual(0, code);
        Assert.Contains("usage: duackcli <command> [options]", err);
    }

    [Fact]
    public void Unknown_command_fails()
    {
        var (code, _, err) = Run("frobnicate");
        Assert.NotEqual(0, code);
        Assert.Contains("frobnicate", err);
    }

    // ---- init (deprecated) ----

    [Fact]
    public void Init_is_deprecated_and_fails()
    {
        var (code, _, err) = Run("init", _dir);

        Assert.Equal(1, code);
        Assert.Contains("deprecated", err, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("legacy/pre-refactor", err);
        Assert.False(File.Exists(Path2("audio-config.json")));
    }

    // ---- validate (deprecated) ----

    [Fact]
    public void Validate_is_deprecated_and_fails()
    {
        var configPath = Path2("audio-config.json");
        File.WriteAllText(configPath, CleanJson());

        var (code, _, err) = Run("validate", configPath);

        Assert.Equal(1, code);
        Assert.Contains("deprecated", err, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("legacy/pre-refactor", err);
    }

    // ---- convert ----

    [Fact]
    public void Convert_without_a_playlist_source_fails()
    {
        TestWav.WriteSine(Path2("theme_a.wav"));
        TestWav.WriteSine(Path2("hit.wav"));

        var (code, _, err) = Run("convert", _dir, Path2("out"));

        Assert.NotEqual(0, code);
        Assert.Contains("--all", err);
        Assert.False(File.Exists(Path2("out", "theme_a.ogg")));
    }

    [Fact]
    public void Convert_with_all_converts_every_wav()
    {
        TestWav.WriteSine(Path2("theme_a.wav"));
        TestWav.WriteSine(Path2("hit.wav"));

        var (code, _, _) = Run("convert", _dir, Path2("out"), "--all");

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path2("out", "theme_a.ogg")));
        Assert.True(File.Exists(Path2("out", "hit.ogg")));
    }

    [Fact]
    public void Convert_with_all_and_a_playlist_source_fails()
    {
        TestWav.WriteSine(Path2("theme_a.wav"));
        var playlistPath = Path2("playlists.json");
        File.WriteAllText(playlistPath, """{ "playlists": {} }""");

        var (code, _, err) = Run("convert", _dir, Path2("out"), "--all", "--playlists", playlistPath);

        Assert.NotEqual(0, code);
        Assert.NotEqual("", err);
    }

    [Fact]
    public void Convert_with_config_fails_as_deprecated()
    {
        TestWav.WriteSine(Path2("theme_a.wav"));
        var configPath = Path2("audio-config.json");
        File.WriteAllText(configPath, CleanJson());

        var (code, _, err) = Run("convert", _dir, Path2("out"), "--config", configPath);

        Assert.Equal(1, code);
        Assert.Contains("deprecated", err, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("legacy/pre-refactor", err);
    }

    [Fact]
    public void Convert_with_playlists_only_converts_songs()
    {
        TestWav.WriteSine(Path2("theme_a.wav"));
        TestWav.WriteSine(Path2("hit.wav"));
        var playlistPath = Path2("playlists.json");
        File.WriteAllText(playlistPath, """
            { "playlists": { "overworld": { "tracks": [ { "name": "theme_a", "context": "Overworld" } ] } } }
            """);

        var (code, _, _) = Run("convert", _dir, Path2("out"), "--playlists", playlistPath);

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path2("out", "theme_a.ogg")));
        Assert.False(File.Exists(Path2("out", "hit.ogg")));
    }

    [Fact]
    public void Convert_skips_up_to_date_outputs_on_the_second_run()
    {
        TestWav.WriteSine(Path2("theme_a.wav"));

        Run("convert", _dir, Path2("out"), "--all");
        var (code, outText, _) = Run("convert", _dir, Path2("out"), "--all");

        Assert.Equal(0, code);
        Assert.Contains("converted=0", outText);
    }

    [Fact]
    public void Convert_copies_a_pre_encoded_ogg_through()
    {
        // A content directory may hold an already-encoded .ogg (a cue with no WAV master).
        // Re-encoding it is impossible and dropping it loses the cue, so it is copied through.
        File.WriteAllBytes(Path2("fire.ogg"), new byte[] { 1, 2, 3, 4 });

        var (code, outText, _) = Run("convert", _dir, Path2("out"), "--all");

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path2("out", "fire.ogg")));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(Path2("out", "fire.ogg")));
        Assert.Contains("copied=1", outText);
    }

    [Fact]
    public void Convert_keeps_a_newer_ogg_output_instead_of_copying_over_it()
    {
        File.WriteAllBytes(Path2("fire.ogg"), new byte[] { 1, 2, 3, 4 });
        Directory.CreateDirectory(Path2("out"));
        File.WriteAllBytes(Path2("out", "fire.ogg"), new byte[] { 9, 9 });
        File.SetLastWriteTimeUtc(Path2("out", "fire.ogg"), DateTime.UtcNow.AddMinutes(1));

        var (code, outText, _) = Run("convert", _dir, Path2("out"), "--all");

        Assert.Equal(0, code);
        Assert.Equal(new byte[] { 9, 9 }, File.ReadAllBytes(Path2("out", "fire.ogg")));
        Assert.Contains("copied=0", outText);
    }


    // ---- scan ----

    [Fact]
    public void Scan_writes_a_manifest_describing_both_manageries()
    {
        Directory.CreateDirectory(Path2("SFX"));
        Directory.CreateDirectory(Path2("Tracks"));
        File.WriteAllBytes(Path2("SFX", "hit.wav"), Array.Empty<byte>());
        File.WriteAllBytes(Path2("SFX", "dialog.wav"), Array.Empty<byte>());
        File.WriteAllBytes(Path2("Tracks", "overworld.ogg"), Array.Empty<byte>());
        File.WriteAllBytes(Path2("Tracks", "battle.ogg"), Array.Empty<byte>());

        var (code, outText, err) = Run("scan", _dir);

        Assert.Equal(0, code);
        Assert.Equal("", err);

        var manifestPath = Path2("audio-manifest.json");
        Assert.True(File.Exists(manifestPath));
        var manifest = AudioLibraryManifestJson.Deserialize(File.ReadAllText(manifestPath));
        Assert.Equal(AudioLibraryManifest.CurrentSchemaVersion, manifest.SchemaVersion);
        Assert.Equal(new[] { "dialog", "hit" }, manifest.Sfx);
        Assert.Equal(new[] { "battle", "overworld" }, manifest.Tracks);

        Assert.Contains("sfx=2", outText);
        Assert.Contains("tracks=2", outText);
    }

    [Fact]
    public void Scan_respects_an_explicit_out_path()
    {
        var outPath = Path2("nested", "manifest.json");

        var (code, _, _) = Run("scan", _dir, "--out", outPath);

        Assert.Equal(0, code);
        Assert.True(File.Exists(outPath));
    }

    [Fact]
    public void Scan_a_missing_audio_root_fails()
    {
        var (code, _, err) = Run("scan", Path2("nope"));

        Assert.Equal(1, code);
        Assert.Contains("nope", err);
    }

    [Fact]
    public void Scan_warns_about_unloadable_files_but_still_succeeds()
    {
        Directory.CreateDirectory(Path2("SFX"));
        Directory.CreateDirectory(Path2("Tracks"));
        File.WriteAllBytes(Path2("SFX", "notes.txt"), Array.Empty<byte>());
        File.WriteAllBytes(Path2("SFX", "hit.wav"), Array.Empty<byte>());
        File.WriteAllBytes(Path2("Tracks", "song.flac"), Array.Empty<byte>());

        var (code, outText, err) = Run("scan", _dir);

        Assert.Equal(0, code); // warnings do not fail the scan
        Assert.Contains("warning:", err);
        Assert.Contains("notes.txt", err);
        Assert.Contains("song.flac", err);
        Assert.Contains("sfx=1", outText);

        var manifest = AudioLibraryManifestJson.Deserialize(File.ReadAllText(Path2("audio-manifest.json")));
        Assert.Equal(new[] { "hit" }, manifest.Sfx);
        Assert.Empty(manifest.Tracks);
    }

    [Fact]
    public void Scan_an_audio_root_without_convention_manageries_writes_an_empty_manifest()
    {
        var (code, outText, err) = Run("scan", _dir);

        Assert.Equal(0, code);
        Assert.Equal("", err);
        Assert.Contains("sfx=0", outText);
        Assert.Contains("tracks=0", outText);

        var manifest = AudioLibraryManifestJson.Deserialize(File.ReadAllText(Path2("audio-manifest.json")));
        Assert.Empty(manifest.Sfx);
        Assert.Empty(manifest.Tracks);
    }

    private static string CleanJson() => """
        {
          "schema_version": 1,
          "master_volume": 1.0,
          "music": {
            "master_volume": 1.0,
            "crossfade_seconds": 1.2,
            "contexts": {},
            "duck_sources": {}
          },
          "sfx": { "master_volume": 1.0 }
        }
        """;
}


