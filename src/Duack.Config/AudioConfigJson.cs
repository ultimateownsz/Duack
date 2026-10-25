using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duack;

/// <summary>
/// The JSON door for <see cref="AudioConfig"/>: parse, serialize, load and save the
/// <c>audio-config.json</c> wire format.
/// <para>
/// This preview is code-only: this is the SECONDARY surface. The mix is authored in code
/// (<see cref="AudioConfigBuilder"/>) and the JSON file is an optional overlay on top of it, not
/// the source of truth. The tooling (<c>duackcli init</c> / <c>duackcli validate</c> /
/// <c>duackcli convert</c>) speaks this format, and so does a game that wants to tune a mix from a
/// file - but nothing in the core requires a file to exist.
/// </para>
/// <para>
/// The format is snake_case, case-insensitive on read, and preserves unknown sections across a
/// load/save cycle (see <see cref="AudioConfig.ExtensionData"/>).
/// </para>
/// </summary>
public static class AudioConfigJson
{
    /// <summary>
    /// Stream source for <see cref="Load"/>/<see cref="TryLoad"/>, keyed by content-relative path.
    /// Defaults to plain file IO (enough for desktop tools); a game replaces this with
    /// TitleContainer.OpenStream so the same relative path resolves on DesktopGL and Blazor.
    /// </summary>
    public static Func<string, Stream> OpenStream { get; set; } = File.OpenRead;

    /// <summary>Deserializes from JSON (snake_case keys, case-insensitive).</summary>
    public static AudioConfig Parse(string json) =>
        JsonSerializer.Deserialize(json, AudioConfigJsonContext.Default.AudioConfig) ?? new AudioConfig();

    /// <summary>
    /// Like <see cref="Parse"/> but returns false (instead of throwing) when the JSON is malformed -
    /// used by the raw-JSON editor in tools to reject bad edits without crashing.
    /// </summary>
    public static bool TryParse(string json, out AudioConfig? config)
    {
        try
        {
            config = Parse(json);
            return config != null;
        }
        catch (JsonException)
        {
            config = null;
            return false;
        }
    }

    /// <summary>Serializes to JSON (snake_case keys; build-only sections preserved).</summary>
    public static string Serialize(AudioConfig config) =>
        JsonSerializer.Serialize(config, AudioConfigJsonContext.Default.AudioConfig);

    /// <summary>
    /// Loads the config from Content/Audio/audio-config.json via <see cref="OpenStream"/>.
    /// Falls back to defaults when the file is missing. Values are clamped by
    /// <see cref="AudioConfig.Sanitize"/> exactly as <see cref="TryLoad"/> does, so a hand-edited
    /// file is clamped identically at boot and on hot reload.
    /// </summary>
    public static AudioConfig Load()
    {
        try
        {
            using var stream = OpenStream("Content/Audio/audio-config.json");
            using var reader = new StreamReader(stream);
            var config = Parse(reader.ReadToEnd());
            config.Sanitize();
            return config;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioConfig] audio-config.json unavailable, using defaults: {ex.Message}");
            return new AudioConfig();
        }
    }

    /// <summary>
    /// Like <see cref="Load"/> but returns null when the file is missing or unreadable.
    /// </summary>
    public static AudioConfig? TryLoad()
    {
        try
        {
            using var stream = OpenStream("Content/Audio/audio-config.json");
            using var reader = new StreamReader(stream);
            var config = Parse(reader.ReadToEnd());
            config.Sanitize();
            return config;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioConfig] audio-config.json unreadable: {ex.Message}");
            return null;
        }
    }

    /// <summary>Writes the config back to disk (no-op on Browser).</summary>
    public static void Save(AudioConfig config, string? path = null)
    {
        if (OperatingSystem.IsBrowser()) return;

        try
        {
            var target = path ?? "Content/Audio/audio-config.json";
            File.WriteAllText(target, Serialize(config));
            Debug.WriteLine($"[AudioConfig] Saved audio config to {target}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioConfig] Save failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Source-generated serialization metadata for <see cref="AudioConfig"/>. Source generation rather
/// than reflection because the engine builds with <c>IsAotCompatible</c>.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(AudioConfig))]
internal partial class AudioConfigJsonContext : JsonSerializerContext;
