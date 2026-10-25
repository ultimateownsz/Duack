using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duack;

/// <summary>
/// The checked-in manifest of an audio root: which sfx and track cues a content directory ships.
/// Browsers cannot enumerate directories, so a web host reads this file instead of scanning;
/// <c>duackcli scan</c> generates it from the two-directory layout. Hand edits are tolerated
/// (snake_case keys, missing lists read as empty, unknown fields survive a load→save cycle).
/// </summary>
public sealed class AudioLibraryManifest
{
    /// <summary>Schema stamp written by this build.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Schema stamp for migrations. A missing stamp reads as <see cref="CurrentSchemaVersion"/>;
    /// a future stamp is preserved as-is rather than downgraded.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>One-shot cue names (the SFX directory's contents).</summary>
    public List<string> Sfx { get; set; } = new();

    /// <summary>Track cue names (the Tracks directory's contents).</summary>
    public List<string> Tracks { get; set; } = new();

    /// <summary>Unknown fields are preserved on a load→save cycle (forward compatibility).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = new();
}

/// <summary>
/// Wire format for <see cref="AudioLibraryManifest"/>, source-generated (not reflection) so AOT
/// web heads can read the manifest at runtime.
/// </summary>
public static class AudioLibraryManifestJson
{
    /// <summary>Serializes a manifest to indented snake_case JSON.</summary>
    public static string Serialize(AudioLibraryManifest manifest) =>
        JsonSerializer.Serialize(manifest, AudioLibraryManifestJsonContext.Default.AudioLibraryManifest);

    /// <summary>
    /// Parses a manifest document. Malformed JSON throws a <see cref="JsonException"/>; explicit
    /// null lists are normalized back to empty so consumers never see one.
    /// </summary>
    public static AudioLibraryManifest Deserialize(string json)
    {
        var manifest = JsonSerializer.Deserialize(json, AudioLibraryManifestJsonContext.Default.AudioLibraryManifest)
            ?? new AudioLibraryManifest();

        // A hand-edited "sfx": null must not reach consumers as a null list.
        manifest.Sfx ??= new List<string>();
        manifest.Tracks ??= new List<string>();
        return manifest;
    }
}

/// <summary>
/// Source-generated serialization metadata for <see cref="AudioLibraryManifest"/>. Source generation
/// rather than reflection because web heads build with <c>RunAOTCompilation</c>.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(AudioLibraryManifest))]
internal partial class AudioLibraryManifestJsonContext : JsonSerializerContext;
