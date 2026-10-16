using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework.Audio;

namespace Duack.MonoGame;

/// <summary>
/// Default <see cref="IAudioClipLibrary"/>: loads whole tracks as <see cref="SoundEffect"/>s from
/// <c>Content/Audio/{name}.{ext}</c> through <c>TitleContainer</c> (file IO on DesktopGL, HTTP fetch
/// on Blazor) and caches them by name. A failed load is NOT cached, so a later call retries.
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
    // OGG-first everywhere: this loader NVorbis-decodes OGG on any backend, so the ordering is
    // independent of which format a head ships. The trailing extension is the fallback.
    private static readonly string[] _extensions = [".ogg", ".wav"];

    private readonly Dictionary<string, IAudioClip?> _cache = new(StringComparer.Ordinal);

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

    private static SoundEffect? Load(string fileName)
    {
        Exception? lastError = null;
        for (var attempt = 0; ; attempt++)
        {
            foreach (var extension in _extensions)
            {
                try
                {
                    using var stream = Microsoft.Xna.Framework.TitleContainer.OpenStream(
                        $"Content/Audio/{fileName}{extension}");
                    return extension == ".ogg"
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