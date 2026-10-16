using Duack;

namespace Duack.Tests.CodeOnly;

public class MusicRetryPolicyTests
{
    [Fact]
    public void Advance_retries_only_after_the_retry_delay_has_elapsed()
    {
        Assert.False(MusicRetryPolicy.Advance(1f, 0.4f, out var remaining));
        Assert.Equal(0.6f, remaining, 3);

        Assert.True(MusicRetryPolicy.Advance(remaining, 0.6f, out remaining));
        Assert.Equal(0f, remaining);
    }
}
