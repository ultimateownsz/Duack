namespace Duack;

/// <summary>
/// Tracks a retry countdown for a failed music load: callers keep the remaining seconds and
/// advance it once per frame. When it reaches zero, the caller should attempt the load again
/// (the caller owns the retry limit/backoff decision).
/// </summary>
public static class MusicRetryPolicy
{
    /// <summary>
    /// Advances <paramref name="remainingSeconds"/> by <paramref name="deltaSeconds"/>, clamped at
    /// zero, and reports whether the countdown has elapsed.
    /// </summary>
    public static bool Advance(float remainingSeconds, float deltaSeconds, out float nextRemainingSeconds)
    {
        nextRemainingSeconds = Math.Max(0f, remainingSeconds - deltaSeconds);
        return nextRemainingSeconds == 0f;
    }
}
