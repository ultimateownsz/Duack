using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework.Audio;

namespace Duack.MonoGame;

/// <summary>
/// Default <see cref="IAudioClipLibrary"/>: loads whole tracks as <see cref="SoundEffect"/>s from
/// <c>Content/Audio/{candidate}</c> through <c>TitleContainer</c> (file IO on DesktopGL, HTTP fetch
/// on Blazor) and caches them by name. A failed load is NOT cached, so a later call retries.
///
/// <para>
/// When constructed with a discovered <see cref="AudioLibrary"/>, probes follow the two-directory
/// convention first (<c>SFX/</c> wav-first for one-shots, <c>Tracks/</c> ogg-first for music) and
/// fall back to the legacy flat layout last (see <see cref="CandidateUris"/>); without a library the
/// probe order is exactly the legacy flat order, so existing content loads unchanged.
/// </para>
///
/// <para>
/// Candidates are tried OGG-first on every backend (decoded here via <see cref="OggPcmWav"/>, since
/// KNI's <c>SoundEffect.FromStream</c> parses RIFF WAV only); the WAV fallback covers desktop's
/// shipped raw masters. Each candidate is opened AND decoded as one unit - a half-written file from
/// an export or a build copy can fail at either stage - both extensions are tried, and one short
/// retry round covers transient rewrites before giving up with a log line and a null result.
/// Loading failures are non-fatal by design: background audio must never take the game down.
/// </para>
/// </summary>
public sealed class SoundEffectClipLibrary : IAudioClipLibrary
{
    private readonly AudioLibrary? _library;
    private readonly Dictionary<string, IAudioClip?> _cache = new(StringComparer.Ordinal);

    /// <param name="library">
    /// Optional discovered cue set; when given, <see cref="AudioLibrary.CandidatePaths"/> order
    /// decides where a cue is looked for. Omit it to keep the legacy flat probe order.
    /// </param>
    public SoundEffectClipLibrary(AudioLibrary? library = null) => _library = library;

    /// <inheritdoc />
    public IAudioClip? Get(string trackName)
    {
        if (_cache.TryGetValue(trackName, out var cached))
            return cached;

        var effect = Load(trackName);
        if (effect == null)
            return null; // Do not cache failures; a later call retries.

        var clip = new SoundEffectAudioClip(trackName, effect);
        _cache[trackName] = clip;
        return clip;
    }

    /// <summary>Drops the cached clip for <paramref name="trackName"/> so the next
    /// <see cref="Get"/> reloads it - used by content hot-reload so an edited audio file takes effect
    /// without a screen restart.</summary>
    public void Invalidate(string trackName)
    {
        if (_cache.Remove(trackName, out var clip))
            clip?.Dispose();
    }

    /// <summary>
    /// The <c>TitleContainer</c> URIs to probe for <paramref name="cueName"/>, in order. With a
    /// library: the owning convention directory first (<c>SFX/</c> wav-first for a discovered sfx,
    /// <c>Tracks/</c> ogg-first for a discovered track, both for an unknown cue), then the legacy
    /// flat layout. Without one: exactly the legacy flat order (<c>.ogg</c>, then <c>.wav</c>).
    /// Pure path composition — no existence check, no IO.
    /// </summary>
    public static IReadOnlyList<string> CandidateUris(AudioLibrary? library, string cueName, string root = "Content/Audio")
    {
        if (library is null)
        {
            return new[] { $"{root}/{cueName}.ogg", $"{root}/{cueName}.wav" };
        }

        var uris = new List<string>();
        foreach (var candidate in library.CandidatePaths(cueName))
            uris.Add($"{root}/{candidate}");
        return uris;
    }

    private SoundEffect? Load(string fileName)
    {
        var candidates = CandidateUris(_library, fileName);
        Exception? lastError = null;
        for (var attempt = 0; ; attempt++)
        {
            foreach (var uri in candidates)
            {
                try
                {
                    using var stream = Microsoft.Xna.Framework.TitleContainer.OpenStream(uri);
                    return Path.GetExtension(uri) == ".ogg"
                        ? SoundEffect.FromStream(OggPcmWav.Decode(stream))
                        : SoundEffect.FromStream(stream);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (attempt > 0)
            {
                Debug.WriteLine($"[Audio] Could not load Content/Audio/{fileName}: {lastError?.Message}");
                return null;
            }

            // Brief pause so a concurrent export/build copy can finish before the retry round.
            System.Threading.Thread.Sleep(50);
        }
    }
}
