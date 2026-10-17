using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duack.Tools;

/// <summary>
/// Reads a <see cref="PlaylistConfig"/> from JSON. Duack's core ships source-generated serializers
/// only for the types the runtime loads (AudioConfig, PlayerAudioSettings); playlists are a
/// build/tooling concern, so this uses the reflection serializer and tolerates hand-edited files
/// (snake_case, string or numeric enums, comments, trailing commas).
/// </summary>
public static class PlaylistFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Parses a standalone playlist JSON document.</summary>
    public static PlaylistConfig Parse(string json) =>
        JsonSerializer.Deserialize<PlaylistConfig>(json, Options) ?? new PlaylistConfig();

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

    /// <summary>
    /// Extracts an embedded <c>playlists</c> section from an audio config's
    /// <see cref="AudioConfig.ExtensionData"/>, or null when the config has none.
    /// </summary>
    public static PlaylistConfig? FromConfig(AudioConfig config)
    {
        if (config.ExtensionData is null ||
            !config.ExtensionData.TryGetValue("playlists", out var element))
        {
            return null;
        }

        try
        {
            // The embedded value is the playlist map itself (the config property is `playlists`),
            // not a document with its own `playlists` wrapper.
            var map = element.Deserialize<Dictionary<string, PlaylistDefinition>>(Options);
            return new PlaylistConfig { Playlists = map ?? new Dictionary<string, PlaylistDefinition>() };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
