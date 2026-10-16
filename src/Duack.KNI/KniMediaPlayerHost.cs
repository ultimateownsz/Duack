using System;
using Microsoft.Xna.Framework.Media;

namespace Duack.KNI;

/// <summary>
/// Production <see cref="IMediaPlayerHost"/> over the static platform <c>MediaPlayer</c>. On
/// BlazorGL <c>Song.FromUri</c> + <c>MediaPlayer.Play</c> drive an HTML <c>&lt;audio&gt;</c> element;
/// the relative URI resolves against the document base URL.
/// </summary>
public sealed class KniMediaPlayerHost : IMediaPlayerHost
{
    /// <inheritdoc />
    public event EventHandler<EventArgs>? StateChanged
    {
        add => MediaPlayer.MediaStateChanged += value;
        remove => MediaPlayer.MediaStateChanged -= value;
    }

    /// <inheritdoc />
    public bool IsStopped => MediaPlayer.State == MediaState.Stopped;

    /// <inheritdoc />
    public bool IsPlaying => MediaPlayer.State == MediaState.Playing;

    /// <inheritdoc />
    public float Volume
    {
        get => MediaPlayer.Volume;
        set => MediaPlayer.Volume = value;
    }

    /// <inheritdoc />
    public bool IsRepeating
    {
        get => MediaPlayer.IsRepeating;
        set => MediaPlayer.IsRepeating = value;
    }

    /// <inheritdoc />
    public void Play(string relativeUri) =>
        MediaPlayer.Play(Song.FromUri(relativeUri, new Uri(relativeUri, UriKind.Relative)));

    /// <inheritdoc />
    public void Stop() => MediaPlayer.Stop();
}