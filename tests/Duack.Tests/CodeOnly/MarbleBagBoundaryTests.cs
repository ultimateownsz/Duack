using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the opt-in boundary guard on <see cref="MarbleBag{T}"/>: when enabled, the last draw of one
/// cycle is never the same item as the first draw of the next, so a shuffled playlist loops without a
/// back-to-back repeat at the seam. The default (off) keeps the plain no-replacement-within-a-cycle
/// behavior.
/// </summary>
public class MarbleBagBoundaryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(13)]
    [InlineData(99)]
    public void Avoiding_immediate_repeat_never_repeats_across_a_cycle_boundary(int seed)
    {
        var bag = new MarbleBag<string>(
            new[] { "a", "b", "c", "d" }, new Random(seed), avoidImmediateRepeat: true);

        var previous = bag.Draw();
        for (var i = 0; i < 64; i++)
        {
            var next = bag.Draw();
            Assert.NotEqual(previous, next);
            previous = next;
        }
    }

    [Fact]
    public void Each_cycle_remains_a_permutation_when_avoiding_repeats()
    {
        var items = new[] { "a", "b", "c", "d", "e" };
        var bag = new MarbleBag<string>(items, new Random(3), avoidImmediateRepeat: true);

        var cycle = Enumerable.Range(0, items.Length).Select(_ => bag.Draw()).ToList();

        Assert.Equal(items.OrderBy(x => x), cycle.OrderBy(x => x));
    }

    [Fact]
    public void A_single_item_bag_still_returns_that_item_when_avoiding_repeats()
    {
        var bag = new MarbleBag<string>(new[] { "solo" }, new Random(5), avoidImmediateRepeat: true);

        Assert.Equal("solo", bag.Draw());
        Assert.Equal("solo", bag.Draw());
    }
}
