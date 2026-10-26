using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duack;

/// <summary>
/// The wire format for <see cref="PlaylistConfig"/>, source-generated (not reflection) so the AOT web
/// head can read playlists at runtime. A standalone playlist document is <c>{ "playlists": { ... } }</c>;
/// the same map may also be embedded in <c>audio-config.json</c> under a <c>playlists</c> key, which is
/// what <see cref="FromConfig"/> reads and <see cref="Embed"/> writes. Hand edits are tolerated:
/// snake_case keys, string or numeric enum values, comments, and trailing commas.
/// <para>
/// Part of the core JSON layer. It exists for tooling and persistence; nothing in the runtime
/// requires a playlist document to exist.
/// </para>
/// </summary>
public static class PlaylistJson
{
    /// <summary>Parses a standalone playlist document (a document with a <c>playlists</c> map).</summary>
    public static PlaylistConfig Parse(string json) =>
        JsonSerializer.Deserialize(json, PlaylistConfigJsonContext.Default.PlaylistConfig) ?? new PlaylistConfig();

    /// <summary>Like <see cref="Parse"/> but returns false instead of throwing on malformed JSON.</summary>
    public static bool TryParse(string json, out PlaylistConfig? playlists)
    {
        try
        {
            playlists = Parse(json);
            return true;
        }
        catch (JsonException)
        {
            playlists = null;
            return false;
        }
    }

    /// <summary>Serializes to snake_case JSON with string enum values.</summary>
    public static string Serialize(PlaylistConfig playlists) =>
        JsonSerializer.Serialize(playlists, PlaylistConfigJsonContext.Default.PlaylistConfig);

    /// <summary>
    /// The bare playlist MAP as JSON (no <c>playlists</c> wrapper), ready to store under the
    /// <c>playlists</c> key of an audio config. See <see cref="Embed"/>.
    /// </summary>
    public static JsonElement ToElement(PlaylistConfig playlists) =>
        JsonSerializer.SerializeToElement(
            playlists.Playlists,
            PlaylistConfigJsonContext.Default.DictionaryStringPlaylistDefinition);

    /// <summary>
    /// Reads a bare playlist map (the value stored under an audio config's <c>playlists</c> key), or
    /// null when the element is not a playlist map.
    /// </summary>
    public static PlaylistConfig? FromElement(JsonElement element)
    {
        try
        {
            var map = element.Deserialize(PlaylistConfigJsonContext.Default.DictionaryStringPlaylistDefinition);
            return new PlaylistConfig { Playlists = map ?? new Dictionary<string, PlaylistDefinition>() };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts the embedded <c>playlists</c> section from an audio config's
    /// <see cref="AudioConfig.ExtensionData"/>, or null when the config has none.
    /// <para><b>Deprecated:</b> embedding playlists into audio-config.json is deprecated.</para>
    /// </summary>
    [System.Obsolete("Embedding playlists into audio-config.json is deprecated and moved to the 'legacy/pre-refactor' branch.")]
    public static PlaylistConfig? FromConfig(AudioConfig config)
    {
        if (config.ExtensionData is null || !config.ExtensionData.TryGetValue("playlists", out var element))
            return null;

        return FromElement(element);
    }

    /// <summary>
    /// Stores <paramref name="playlists"/> in an audio config's <see cref="AudioConfig.ExtensionData"/>
    /// under the <c>playlists</c> key, so a later <c>AudioConfigJson.Serialize</c> emits it and
    /// <see cref="FromConfig"/> reads it back.
    /// <para><b>Deprecated:</b> embedding playlists into audio-config.json is deprecated.</para>
    /// </summary>
    [System.Obsolete("Embedding playlists into audio-config.json is deprecated and moved to the 'legacy/pre-refactor' branch.")]
    public static void Embed(AudioConfig config, PlaylistConfig playlists)
    {
        config.ExtensionData ??= new Dictionary<string, JsonElement>();
        config.ExtensionData["playlists"] = ToElement(playlists);
    }
}

/// <summary>
/// Source-generated serialization metadata for playlists. Source generation rather than reflection
/// because the engine builds with <c>IsAotCompatible</c>. The embedded map value the config carries
/// is the playlist map itself, so both the wrapper document and the bare map are registered.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(PlaylistConfig))]
[JsonSerializable(typeof(Dictionary<string, PlaylistDefinition>))]
internal partial class PlaylistConfigJsonContext : JsonSerializerContext;
