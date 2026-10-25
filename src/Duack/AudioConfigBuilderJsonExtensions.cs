namespace Duack;

/// <summary>
/// The JSON-only additions to <see cref="AudioConfigBuilder"/>.
/// <para>
/// Embedding playlists into a config is a WIRE-FORMAT concern, not a mixing concern: the embedded
/// <c>playlists</c> section exists so the CLI (<c>duackcli convert --config</c>) can find the songs a
/// game plays. It lives with the core builder because the JSON layer is part of the core package.
/// </para>
/// </summary>
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
