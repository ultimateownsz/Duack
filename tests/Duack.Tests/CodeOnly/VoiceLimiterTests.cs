using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Slice A: global and per-cue voice caps, stealing policies, and explicit refusal reasons.
/// The limiter is pure core — no backend, no platform timers.
/// </summary>
public class VoiceLimiterTests
{
    [Fact]
    public void Global_cap_steals_the_oldest_voice_at_the_boundary()
    {
        var limiter = new VoiceLimiter(new VoiceLimitConfig { GlobalMaxVoices = 2 });

        Assert.Equal(VoicePlayOutcome.Played, limiter.Acquire("hit", 1f, 1).Outcome);
        Assert.Equal(VoicePlayOutcome.Played, limiter.Acquire("hit", 1f, 2).Outcome);
        var third = limiter.Acquire("hit", 1f, 3);

        Assert.Equal(VoicePlayOutcome.Played, third.Outcome);
        Assert.Equal(1, third.StoleSequence);
        Assert.Equal(new[] { 2L, 3L }, limiter.Active.Select(v => v.Sequence).OrderBy(x => x));
    }

    [Fact]
    public void Quietest_policy_steals_the_lowest_volume_voice_with_oldest_as_tiebreak()
    {
        var config = new VoiceLimitConfig
        {
            GlobalMaxVoices = 2,
            DefaultStealingPolicy = VoiceStealingPolicy.Quietest,
        };
        var limiter = new VoiceLimiter(config);

        limiter.Acquire("hit", 0.9f, 1);
        limiter.Acquire("hit", 0.2f, 2);
        var third = limiter.Acquire("hit", 0.5f, 3);

        Assert.Equal(2L, third.StoleSequence);
    }

    [Fact]
    public void Priority_policy_rejects_a_new_voice_below_the_weakest_active_priority()
    {
        var config = new VoiceLimitConfig
        {
            GlobalMaxVoices = 2,
            DefaultStealingPolicy = VoiceStealingPolicy.Priority,
        };
        var limiter = new VoiceLimiter(config);

        limiter.Acquire("hit", 1f, 1, priority: 10);
        limiter.Acquire("hit", 1f, 2, priority: 5);
        var weak = limiter.Acquire("hit", 1f, 3, priority: 4);
        var strong = limiter.Acquire("hit", 1f, 4, priority: 11);

        Assert.Equal(VoicePlayOutcome.RejectedBelowPriority, weak.Outcome);
        Assert.Equal(VoicePlayOutcome.Played, strong.Outcome);
        Assert.Equal(2L, strong.StoleSequence);
    }

    [Fact]
    public void Per_cue_cap_overrides_the_global_cap()
    {
        var config = new VoiceLimitConfig { GlobalMaxVoices = 10 };
        config.CueLimits["hit"] = new CueVoiceLimit { MaxVoices = 1 };
        var limiter = new VoiceLimiter(config);

        limiter.Acquire("hit", 1f, 1);
        var second = limiter.Acquire("hit", 1f, 2);
        limiter.Acquire("other", 1f, 3);
        limiter.Acquire("other", 1f, 4);

        Assert.Equal(1, second.StoleSequence);
        Assert.Equal(1, limiter.Active.Count(v => v.Cue == "hit"));
        Assert.Equal(2, limiter.Active.Count(v => v.Cue == "other"));
    }

    [Fact]
    public void A_zero_cap_means_unlimited_voices()
    {
        var limiter = new VoiceLimiter(new VoiceLimitConfig { GlobalMaxVoices = 0 });

        for (var i = 1; i <= 20; i++)
            Assert.Equal(VoicePlayOutcome.Played, limiter.Acquire("hit", 1f, i).Outcome);

        Assert.Equal(20, limiter.Active.Count);
    }

    [Fact]
    public void Voices_expire_when_aged_past_their_duration()
    {
        var limiter = new VoiceLimiter(new VoiceLimitConfig
        {
            GlobalMaxVoices = 1,
            DefaultVoiceSeconds = 0.5f,
        });

        limiter.Acquire("hit", 1f, 1);
        limiter.Update(0.49f);
        Assert.Single(limiter.Active);
        limiter.Update(0.02f);
        Assert.Empty(limiter.Active);
    }

    [Fact]
    public void Release_removes_a_voice_whose_backend_refused_the_play()
    {
        var limiter = new VoiceLimiter(new VoiceLimitConfig { GlobalMaxVoices = 1 });

        var accepted = limiter.Acquire("hit", 1f, 1);
        limiter.Release(accepted);
        var next = limiter.Acquire("hit", 1f, 2);

        Assert.Equal(2, next.VoiceSequence);
        Assert.Equal(0, next.StoleSequence);
    }

    [Fact]
    public void Per_cue_lifetime_and_policy_overrides_win_over_the_defaults()
    {
        var config = new VoiceLimitConfig
        {
            GlobalMaxVoices = 2,
            DefaultVoiceSeconds = 10f,
            DefaultStealingPolicy = VoiceStealingPolicy.Oldest,
        };
        config.CueLimits["tick"] = new CueVoiceLimit
        {
            MaxVoices = 2,
            DurationSeconds = 0.25f,
            StealingPolicy = VoiceStealingPolicy.Quietest,
        };
        var limiter = new VoiceLimiter(config);

        limiter.Acquire("tick", 0.9f, 1);
        limiter.Acquire("tick", 0.2f, 2);

        // Quietest override: the quiet voice is stolen, not the oldest (sequence 1).
        var third = limiter.Acquire("tick", 0.5f, 3);
        Assert.Equal(2L, third.StoleSequence);

        // Lifetime override: the tick slots expire at 0.25 s while a default-lifetime cue persists.
        limiter.Acquire("hold", 1f, 4);
        limiter.Update(0.26f);

        Assert.Empty(limiter.Active.Where(v => v.Cue == "tick"));
        Assert.Single(limiter.Active.Where(v => v.Cue == "hold"));
    }
}
