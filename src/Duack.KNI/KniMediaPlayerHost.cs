using System;
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
}
