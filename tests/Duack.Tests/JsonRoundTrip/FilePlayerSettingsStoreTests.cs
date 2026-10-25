using Duack;
using Xunit;

namespace Duack.Tests.JsonRoundTrip;

/// <summary>
/// Pins the desktop half of Plan C: the per-user settings file round-trips through real JSON on
/// disk, survives a corrupt file without wedging the game, reports write failure instead of
/// throwing, and leaves no temporary files behind.
/// </summary>
public class FilePlayerSettingsStoreTests
{
    private sealed class TempRoot : IDisposable
    {
        public string Path { get; }

        public TempRoot()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "duack-player-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // A leftover temp dir is not worth failing a test over.
            }
        }
    }

    [Fact]
    public void FilePlayerSettingsStore_defaults_to_a_per_user_root_outside_the_content_managery()
    {
        var store = new FilePlayerSettingsStore();

        Assert.DoesNotContain("Content", store.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.CurrentDirectory, store.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FilePlayerSettingsStore_default_root_uses_the_host_application_name()
    {
        // The store must not pick a consumer's folder name. The host application owns that
        // identity; a library that hardcodes one game's name leaks that game into every adopter.
        var expected = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "Duack";

        var store = new FilePlayerSettingsStore();

        Assert.Equal(expected, System.IO.Path.GetFileName(store.RootDirectory));
    }

    [Fact]
    public void FilePlayerSettingsStore_round_trips_through_its_file()
    {
        using var root = new TempRoot();
        var store = new FilePlayerSettingsStore(root.Path);

        Assert.True(store.Save(new PlayerAudioSettings
        {
            MusicMasterVolume = 0.3f,
            SfxMasterVolume = 0.6f,
            CrossfadeSeconds = 2.5f
        }));
        Assert.True(File.Exists(store.FilePath));

        var reopened = new FilePlayerSettingsStore(root.Path);
        Assert.True(reopened.TryLoad(out var loaded));
        Assert.Equal(0.3f, loaded!.MusicMasterVolume);
        Assert.Equal(0.6f, loaded.SfxMasterVolume);
        Assert.Equal(2.5f, loaded.CrossfadeSeconds);
    }

    [Fact]
    public void FilePlayerSettingsStore_TryLoad_is_false_when_nothing_was_saved()
    {
        using var root = new TempRoot();
        var store = new FilePlayerSettingsStore(root.Path);

        Assert.False(store.TryLoad(out var loaded));
        Assert.Null(loaded);
    }

    [Fact]
    public void FilePlayerSettingsStore_recovers_from_a_corrupt_file_instead_of_throwing()
    {
        using var root = new TempRoot();
        File.WriteAllText(Path.Combine(root.Path, "player-settings.json"), "{ this is not json");
        var store = new FilePlayerSettingsStore(root.Path);

        Assert.False(store.TryLoad(out var loaded));
        Assert.Null(loaded);

        // A corrupt file must not wedge the game: the next save overwrites it.
        Assert.True(store.Save(new PlayerAudioSettings { MusicMasterVolume = 0.5f }));
        Assert.True(store.TryLoad(out var reloaded));
        Assert.Equal(0.5f, reloaded!.MusicMasterVolume);
    }

    [Fact]
    public void FilePlayerSettingsStore_Save_reports_failure_instead_of_throwing()
    {
        using var root = new TempRoot();
        // A file where the settings directory should be makes every write fail.
        var blocked = Path.Combine(root.Path, "blocked");
        File.WriteAllText(blocked, "not a directory");
        var store = new FilePlayerSettingsStore(blocked);

        Assert.False(store.Save(new PlayerAudioSettings()));
        Assert.False(store.TryLoad(out _));
    }

    [Fact]
    public void FilePlayerSettingsStore_leaves_no_temporary_files_behind()
    {
        using var root = new TempRoot();
        var store = new FilePlayerSettingsStore(root.Path);

        store.Save(new PlayerAudioSettings { MusicMasterVolume = 0.5f });

        Assert.Equal(new[] { "player-settings.json" }, Directory.GetFiles(root.Path).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void FilePlayerSettingsStore_Clear_removes_the_file()
    {
        using var root = new TempRoot();
        var store = new FilePlayerSettingsStore(root.Path);
        store.Save(new PlayerAudioSettings());

        store.Clear();

        Assert.False(store.TryLoad(out _));
    }
}
