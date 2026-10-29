using Duack;
namespace DuackCli;

/// <summary>
/// Thin CLI-facing wrapper over the core <see cref="PlaylistJson"/> wire format. Playlists are a
/// build/tooling concern, so the reader lives here; the format itself is defined once in core so the
/// tool and the runtime never drift (and the runtime stays AOT-safe via source generation).
/// </summary>
public static class PlaylistFile
{
    /// <summary>Parses a standalone playlist JSON document.</summary>
    public static PlaylistConfig Parse(string json) => PlaylistJson.Parse(json);

    /// <summary>Like <see cref="Parse"/> but returns false instead of throwing on malformed JSON.</summary>
    public static bool TryParse(string json, out PlaylistConfig? playlists) =>
        PlaylistJson.TryParse(json, out playlists);

}

