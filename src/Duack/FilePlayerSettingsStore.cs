using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Duack;

/// <summary>
/// Per-user store for the desktop heads. Writes outside the content directory on purpose: the
/// authored <c>Content/Audio/audio-config.json</c> ships with the game and a build or an ffmpeg
/// re-export can regenerate it, so player preferences cannot live there. The old config-save path
/// was also CWD-relative, which meant it only resolved next to the executable when the process
/// happened to be launched from that directory.
/// </summary>
public class FilePlayerSettingsStore : IPlayerSettingsStore
{
    /// <summary>File name inside <see cref="RootDirectory"/>.</summary>
    public const string FileName = "player-settings.json";

    private const string TempSuffix = ".tmp";

    public FilePlayerSettingsStore(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? DefaultRootDirectory();
        FilePath = Path.Combine(RootDirectory, FileName);
    }

    /// <summary>Directory holding <see cref="FilePath"/>, created on first write.</summary>
    public string RootDirectory { get; }

    /// <summary>Full path of the settings file.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Per-user application data, e.g. <c>%APPDATA%\{host assembly}</c> on Windows and
    /// <c>~/.config/{host assembly}</c> elsewhere. The folder is named after the host application,
    /// not this library or any particular consumer. Falls back to the temp directory when the OS
    /// reports no application-data folder; the store then simply reports failure to save.
    /// </summary>
    public static string DefaultRootDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrEmpty(appData))
        {
            Debug.WriteLine("[PlayerSettings] no application data folder, falling back to temp");
            appData = Path.GetTempPath();
        }

        return Path.Combine(appData, ResolveApplicationName());
    }

    /// <summary>
    /// Name of the folder under the per-user application-data directory. Uses the entry assembly so
    /// each host game gets its own store; falls back to <c>Duack</c> when the runtime does not expose
    /// an entry assembly (for example, a native host).
    /// </summary>
    private static string ResolveApplicationName() =>
        Assembly.GetEntryAssembly()?.GetName().Name ?? "Duack";

    public bool TryLoad(out PlayerAudioSettings? settings)
    {
        settings = null;

        try
        {
            if (!File.Exists(FilePath)) return false;

            var json = File.ReadAllText(FilePath);
            var parsed = JsonSerializer.Deserialize(json, PlayerAudioSettingsJsonContext.Default.PlayerAudioSettings);
            if (parsed == null) return false;

            parsed.Sanitize();
            settings = parsed;
            return true;
        }
        catch (Exception ex)
        {
            // A corrupt or half-written file must not wedge the game: the overlay keeps its current
            // values and the next save overwrites it.
            Debug.WriteLine($"[PlayerSettings] read failed for {FilePath}: {ex.Message}");
            settings = null;
            return false;
        }
    }

    public bool Save(PlayerAudioSettings settings)
    {
        try
        {
            settings.Sanitize();

            Directory.CreateDirectory(RootDirectory);

            // Write beside the target and move into place so an interrupted write cannot leave a
            // truncated settings file behind.
            var temp = FilePath + TempSuffix;
            File.WriteAllText(temp, JsonSerializer.Serialize(
                settings, PlayerAudioSettingsJsonContext.Default.PlayerAudioSettings));
            File.Move(temp, FilePath, overwrite: true);

            Debug.WriteLine($"[PlayerSettings] saved to {FilePath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlayerSettings] save failed for {FilePath}: {ex.Message}");
            return false;
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlayerSettings] clear failed for {FilePath}: {ex.Message}");
        }
    }
}
