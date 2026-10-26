using System;

namespace Duack;

/// <summary>
/// The JSON additions to <see cref="AudioConfigBuilder"/>.
/// <para>
/// <b>Deprecated:</b> Embedding playlists into an <c>audio-config.json</c> is deprecated. Use standalone
/// playlist configuration (<see cref="PlaylistConfigBuilder"/>) or the two-directory audio convention.
/// </para>
/// </summary>
[Obsolete("audio-config.json is deprecated and moved to the 'legacy/pre-refactor' branch. Use code-first AudioConfigBuilder and two-directory convention instead.")]
public static class AudioConfigBuilderJsonExtensions
{
    /// <summary>
    /// Attaches code-authored <paramref name="playlists"/> to the config by embedding them under the
    /// <c>playlists</c> key, so <see cref="AudioConfigBuilder.Build"/> then
    /// <c>AudioConfigJson.Serialize</c> emits a config the tooling can read back with
    /// <c>PlaylistJson.FromConfig</c>.
    /// </summary>
    public static AudioConfigBuilder WithPlaylists(this AudioConfigBuilder builder, PlaylistConfig playlists)
    {
        // Build() hands back the very instance the builder keeps, so embedding into it is what the
        // caller's own Build() returns.
        PlaylistJson.Embed(builder.Build(), playlists);
        return builder;
    }
}
