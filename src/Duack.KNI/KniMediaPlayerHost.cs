using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Media;

namespace Duack.KNI;

public sealed class KniMediaPlayerHost : IMediaPlayerHost
{
    public event EventHandler<EventArgs>? StateChanged
    {
        add { try { FrameworkDispatcher.Update(); MediaPlayer.MediaStateChanged += value; } catch { } }
        remove { try { MediaPlayer.MediaStateChanged -= value; } catch { } }
    }

    public bool IsStopped
    {
        get { try { FrameworkDispatcher.Update(); return MediaPlayer.State == MediaState.Stopped; } catch { return true; } }
    }

    public bool IsPlaying
    {
        get { try { FrameworkDispatcher.Update(); return MediaPlayer.State == MediaState.Playing; } catch { return false; } }
    }

    public float Volume
    {
        get { try { FrameworkDispatcher.Update(); return MediaPlayer.Volume; } catch { return 1f; } }
        set { try { FrameworkDispatcher.Update(); MediaPlayer.Volume = value; } catch { } }
    }

    public bool IsRepeating
    {
        get { try { FrameworkDispatcher.Update(); return MediaPlayer.IsRepeating; } catch { return false; } }
        set { try { FrameworkDispatcher.Update(); MediaPlayer.IsRepeating = value; } catch { } }
    }

    public void Play(string relativeUri)
    {
        try { FrameworkDispatcher.Update(); MediaPlayer.Play(Song.FromUri(relativeUri, new Uri(relativeUri, UriKind.Relative))); }
        catch { }
    }

    public void Stop()
    {
        try { FrameworkDispatcher.Update(); MediaPlayer.Stop(); }
        catch { }
    }

    public void Pause()
    {
        try { FrameworkDispatcher.Update(); MediaPlayer.Pause(); }
        catch { }
    }

    public void Resume()
    {
        try { FrameworkDispatcher.Update(); MediaPlayer.Resume(); }
        catch { }
    }

    public TimeSpan Position
    {
        get { try { FrameworkDispatcher.Update(); return MediaPlayer.PlayPosition; } catch { return TimeSpan.Zero; } }
    }

    public TimeSpan Duration
    {
        get
        {
            try { FrameworkDispatcher.Update(); return MediaPlayer.Queue.ActiveSong.Duration; }
            catch { return TimeSpan.Zero; }
        }
    }

    /// <summary>
    /// KNI's managed <c>MediaPlayer</c> exposes <c>PlayPosition</c> read-only and no seek at all —
    /// but on the browser the strategy drives an HTML <c>&lt;audio&gt;</c> element whose
    /// <c>currentTime</c> is settable from JS. The element object is package-internal (a private
    /// field on the media strategy, several assemblies deep), so this walks to it by reflection
    /// and calls its <c>fastSeek</c> through the element's JS-object dispatch. Every link that can
    /// break is wrapped: a KNI update that moves the field, a seek during load, or a browser that
    /// refuses the call all return false (reported by the caller) rather than throwing into the
    /// frame. Revisit if KNI ships a managed seek; the reflection here is the only coupling.
    /// </summary>
    public bool TrySeek(TimeSpan position)
    {
        try
        {
            FrameworkDispatcher.Update();

            var player = typeof(MediaPlayer)
                .GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null);
            if (player is null) return false;

            var strategy = player.GetType()
                .GetField("_strategy", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(player);
            if (strategy is null) return false;

            var webPlayer = strategy.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(f => f.FieldType.Name == "Audio")
                ?.GetValue(strategy);
            if (webPlayer is null) return false;

            var invoke = webPlayer.GetType().GetMethod("Invoke",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                new[] { typeof(string), typeof(double) });
            if (invoke is null) return false;

            invoke.Invoke(webPlayer, new object[] { "fastSeek", position.TotalSeconds });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
