using System.Collections.Generic;

namespace Duack;

/// <summary>How a full voice slot is freed (or a new play refused) when a cap is reached.</summary>
public enum VoiceStealingPolicy
{
    /// <summary>Steal the voice that started first (the default).</summary>
    Oldest,

    /// <summary>Steal the quietest active voice; oldest wins ties.</summary>
    Quietest,

    /// <summary>Refuse a new voice below the weakest active priority; otherwise steal that one.</summary>
    Priority,
}

/// <summary>Per-cue override of the global voice limit.</summary>
public sealed class CueVoiceLimit
{
    /// <summary>Maximum simultaneous voices for this cue. 0 means unlimited.</summary>
    public int MaxVoices { get; set; }

    /// <summary>Priority used by <see cref="VoiceStealingPolicy.Priority"/>; higher wins.</summary>
    public int Priority { get; set; }

    /// <summary>Assumed lifetime in seconds; overrides <see cref="VoiceLimitConfig.DefaultVoiceSeconds"/>.</summary>
    public float? DurationSeconds { get; set; }

    /// <summary>Optional per-cue stealing policy; overrides the config default.</summary>
    public VoiceStealingPolicy? StealingPolicy { get; set; }
}

/// <summary>Authored limits for simultaneous SFX voices.</summary>
public sealed class VoiceLimitConfig
{
    /// <summary>Maximum simultaneous SFX voices across all cues. 0 (the default) means unlimited.</summary>
    public int GlobalMaxVoices { get; set; }

    /// <summary>
    /// Assumed one-shot lifetime in seconds. Backends report no voice-end event, so the limiter
    /// ages voices with <see cref="VoiceLimiter.Update"/>; the default is intentionally short.
    /// </summary>
    public float DefaultVoiceSeconds { get; set; } = 0.5f;

    /// <summary>Default stealing policy when neither config nor cue overrides it.</summary>
    public VoiceStealingPolicy DefaultStealingPolicy { get; set; } = VoiceStealingPolicy.Oldest;

    /// <summary>Per-cue overrides keyed by cue name.</summary>
    public Dictionary<string, CueVoiceLimit> CueLimits { get; set; } = new();

    /// <summary>Clamps values to safe ranges.</summary>
    public void Sanitize()
    {
        GlobalMaxVoices = System.Math.Max(0, GlobalMaxVoices);
        DefaultVoiceSeconds = System.Math.Max(0f, DefaultVoiceSeconds);
        foreach (var cue in CueLimits.Values)
        {
            cue.MaxVoices = System.Math.Max(0, cue.MaxVoices);
            if (cue.DurationSeconds is { } seconds) cue.DurationSeconds = System.Math.Max(0f, seconds);
        }
    }
}

/// <summary>Observable outcome of an SFX play after validation, limits, and backend refusal.</summary>
public enum SfxPlayResult
{
    /// <summary>The cue reached the backend and plays (possibly by stealing another voice).</summary>
    Played,

    /// <summary>The cue name was not discovered in the audio library.</summary>
    UnknownCue,

    /// <summary>The cue was refused because every slot belongs to a higher-priority voice.</summary>
    RejectedBelowPriority,

    /// <summary>The backend refused to play the cue.</summary>
    BackendRefused,
}

/// <summary>Why a voice was (or was not) admitted.</summary>
public enum VoicePlayOutcome
{
    /// <summary>The voice plays (possibly by stealing an older or quieter one).</summary>
    Played,

    /// <summary>A higher-or-equal priority voice already owns every slot.</summary>
    RejectedBelowPriority,
}

/// <summary>An active (or stolen) voice recorded by the limiter.</summary>
/// <param name="Cue">Cue name that owns the voice.</param>
/// <param name="Volume">Final playback volume, used by the quietest policy.</param>
/// <param name="Sequence">Monotonic start order, used as the oldest tiebreak.</param>
/// <param name="Priority">Authored priority, used by the priority policy.</param>
/// <param name="RemainingSeconds">Time left before the voice releases its slot.</param>
public readonly record struct ActiveVoice(
    string Cue, float Volume, long Sequence, int Priority, float RemainingSeconds);

/// <summary>Result of a voice-admission attempt.</summary>
/// <param name="Outcome">Whether the voice was admitted.</param>
/// <param name="StoleSequence">Sequence of the stolen voice, or 0 when nothing was stolen.</param>
/// <param name="VoiceSequence">Sequence assigned to the admitted voice, or 0 when refused.</param>
public readonly record struct VoiceAcquireResult(
    VoicePlayOutcome Outcome, long StoleSequence, long VoiceSequence);
