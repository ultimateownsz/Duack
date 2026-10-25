using System.Text.Json;

namespace Duack;

/// <summary>
/// Minimal key/value string storage abstraction that lets the web store be unit-tested without
/// hitting <c>IJSRuntime</c>. The KNI flavor wires this to <c>localStorage</c>.
/// </summary>
public interface IKeyValueStorage
{
    /// <summary>Returns the stored value, or null if not present. Throws on read failures.</summary>
    string? Get(string key);

    /// <summary>Writes the value and returns true on success. Throws on write failures.</summary>
    bool Set(string key, string value);

    /// <summary>Removes the value. Does not throw.</summary>
    void Remove(string key);
}

/// <summary>
/// The core logic of the browser store, independent of JS interop. It only deals with JSON and
/// clamping, so tests can exercise every failure path by swapping <see cref="IKeyValueStorage"/>.
/// </summary>
public class WebPlayerSettingsStore : IPlayerSettingsStore
{
    public const string DefaultKey = "duack:playerAudioSettings";

    private readonly IKeyValueStorage _storage;
    private readonly string _key;

    public WebPlayerSettingsStore(IKeyValueStorage storage, string? key = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _key = string.IsNullOrEmpty(key) ? DefaultKey : key;
    }

    public bool TryLoad(out PlayerAudioSettings? settings)
    {
        settings = null;
        try
        {
            var json = _storage.Get(_key);
            if (string.IsNullOrWhiteSpace(json)) return false;

            var parsed = JsonSerializer.Deserialize(
                json,
                PlayerAudioSettingsJsonContext.Default.PlayerAudioSettings);
            if (parsed == null) return false;

            parsed.Sanitize();
            settings = parsed;
            return true;
        }
        catch (Exception ex)
        {
            // Malformed stored JSON (a hand-edited localStorage entry, a truncated write) must not
            // break boot, but silently reverting to the authored defaults looks identical to "no
            // settings saved yet", so the reason is reported. Console.WriteLine, not Debug.WriteLine:
            // Debug is [Conditional("DEBUG")] and this is a shipping build, where the call and its
            // message are removed entirely. Pinned by SettingsDiagnosticsVisibilityTests.
            Console.WriteLine(
                $"[Settings] stored settings for '{_key}' could not be read; using authored " +
                $"defaults. The stored value is likely corrupt. Cause: {ex.Message}");
            settings = null;
            return false;
        }
    }

    public bool Save(PlayerAudioSettings settings)
    {
        try
        {
            settings.Sanitize();
            var json = JsonSerializer.Serialize(
                settings,
                PlayerAudioSettingsJsonContext.Default.PlayerAudioSettings);
            _storage.Set(_key, json);
            return true;
        }
        catch (Exception ex)
        {
            // Same reasoning as TryLoad's catch, and it matters more here: this verdict is the one
            // the overlay turns into its red "Save failed" flash, so this catch is the last place the
            // cause exists before it is thrown away. It shipped as a bare `catch { return false; }`
            // while the only other report (Debug.WriteLine in PlayerSettings) was stripped from
            // Release, which made a genuine web save failure undiagnosable - the browser console had
            // nothing at all to show for it. Console.WriteLine, not Debug.WriteLine: see the comment
            // in TryLoad and Tests/Build/SettingsDiagnosticsVisibilityTests.cs.
            Console.WriteLine(
                $"[Settings] saving '{_key}' failed; any previously stored settings were left " +
                $"untouched. If this happens every time, volume changes will not survive a reload. " +
                $"Cause: {ex.Message}");
            return false;
        }
    }

    public void Clear()
    {
        try
        {
            _storage.Remove(_key);
        }
        catch
        {
            // Never throw during cleanup.
        }
    }
}
