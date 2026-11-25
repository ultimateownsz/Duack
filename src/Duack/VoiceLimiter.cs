using System;
using System.Collections.Generic;
using System.Linq;

namespace Duack;

/// <summary>
/// Tracks assumed active SFX voices and enforces global/per-cue caps. Pure BCL: the backend owns
/// playback, the limiter owns the arithmetic. A play that cannot get a slot returns a reason the
/// caller can report instead of disappearing silently.
/// </summary>
public sealed class VoiceLimiter
{
    private readonly VoiceLimitConfig _config;
    private readonly List<ActiveVoice> _active = new();
    private long _sequence;

    public VoiceLimiter(VoiceLimitConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        config.Sanitize();
    }

    /// <summary>Read-only view of the currently reserved voices.</summary>
    public IReadOnlyList<ActiveVoice> Active => _active;

    /// <summary>Number of currently reserved voices.</summary>
    public int ActiveVoiceCount => _active.Count;

    /// <summary>Reserves a slot for <paramref name="cue"/>, stealing a victim when a cap is full.</summary>
    public VoiceAcquireResult Acquire(string cue, float volume, long sequence = 0, int priority = 0)
    {
        if (sequence <= 0) sequence = ++_sequence;
        else if (sequence > _sequence) _sequence = sequence;

        var cueLimit = _config.CueLimits.TryGetValue(cue, out var limit) ? limit : null;
        var cueCap = cueLimit?.MaxVoices ?? _config.GlobalMaxVoices;
        var policy = cueLimit?.StealingPolicy ?? _config.DefaultStealingPolicy;
        var lifetime = cueLimit?.DurationSeconds ?? _config.DefaultVoiceSeconds;

        var stolen = 0L;
        if (cueCap > 0)
        {
            var onCue = _active.Where(v => v.Cue == cue).ToList();
            if (onCue.Count >= cueCap)
            {
                var victim = ChooseVictim(onCue, policy, priority);
                if (victim is null)
                    return new VoiceAcquireResult(VoicePlayOutcome.RejectedBelowPriority, 0, 0);
                stolen = victim.Value.Sequence;
                _active.Remove(victim.Value);
            }
        }

        if (_config.GlobalMaxVoices > 0 && _active.Count >= _config.GlobalMaxVoices)
        {
            var victim = ChooseVictim(_active, policy, priority);
            if (victim is null)
                return new VoiceAcquireResult(VoicePlayOutcome.RejectedBelowPriority, 0, 0);
            if (stolen == 0) stolen = victim.Value.Sequence;
            _active.Remove(victim.Value);
        }

        _active.Add(new ActiveVoice(cue, volume, sequence, priority, lifetime));
        return new VoiceAcquireResult(VoicePlayOutcome.Played, stolen, sequence);
    }

    /// <summary>Releases a reservation, e.g. when the backend refuses the play.</summary>
    public void Release(VoiceAcquireResult acquired)
    {
        if (acquired.Outcome != VoicePlayOutcome.Played) return;
        Release(acquired.VoiceSequence);
    }

    /// <summary>Releases a reservation by its start sequence.</summary>
    public void Release(long sequence)
    {
        var voice = _active.FirstOrDefault(v => v.Sequence == sequence);
        if (voice.Sequence != 0) _active.Remove(voice);
    }

    /// <summary>Ages reservations and frees expired voices.</summary>
    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0f) return;
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var voice = _active[i];
            var remaining = voice.RemainingSeconds - deltaSeconds;
            if (remaining <= 0f) _active.RemoveAt(i);
            else _active[i] = voice with { RemainingSeconds = remaining };
        }
    }

    /// <summary>Forgets all active voices (scene change).</summary>
    public void Clear() => _active.Clear();

    private static ActiveVoice? ChooseVictim(
        IReadOnlyCollection<ActiveVoice> candidates, VoiceStealingPolicy policy, int newPriority)
    {
        if (candidates.Count == 0) return null;
        if (policy == VoiceStealingPolicy.Quietest)
            return candidates.OrderBy(v => v.Volume).ThenBy(v => v.Sequence).First();

        if (policy == VoiceStealingPolicy.Priority)
        {
            var weakest = candidates.MinBy(v => v.Priority)!;
            if (newPriority <= weakest.Priority) return null;
            return weakest;
        }

        return candidates.MinBy(v => v.Sequence);
    }
}
