using System;

namespace Duack;

/// <summary>
/// Optional <see cref="IAudioClip"/> capability: creating playback instances that start at an
/// offset into the track. Additive, the same pattern as <see cref="ISfxPositionalBackend"/>: a
/// clip that does not implement it is reported by <see cref="IMusicBackend.SupportsSeek"/> as
/// false and <see cref="IMusicBackend.Seek"/> stays a fail-soft no-op.
/// </summary>
public interface ISeekableAudioClip : IAudioClip
{
    /// <summary>
    /// Creates an instance whose playback starts at <paramref name="startOffset"/> (the audible
    /// position, not a samples guarantee). Null means the offset could not be honored (the decode
    /// failed or the offset is at/past the track's end); the caller keeps playing what it has.
    /// Volume and pitch of the returned instance are unset — the backend re-applies them.
    /// </summary>
    IAudioInstance? CreateInstance(TimeSpan startOffset);

    /// <summary>
    /// Releases the transient resources this clip paired with <paramref name="instance"/> — an
    /// offset-created instance may own a dedicated decoded buffer that is not in the clip cache.
    /// A no-op for instances whose resources are shared (cached effects).
    /// </summary>
    void Release(IAudioInstance instance);
}
