using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duack;

/// <summary>
/// The wire format for <see cref="PlaylistConfig"/>, source-generated (not reflection) so the AOT web
/// head can read playlists at runtime. A standalone playlist document is
/// <c>{ "playlists": { ... } }</c>. Hand edits are tolerated: snake_case keys, string or numeric
/// enum values, comments, and trailing commas.
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

}

/// <summary>
/// Source-generated serialization metadata for playlists. Source generation rather than reflection
/// because the engine builds with <c>IsAotCompatible</c>.
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
