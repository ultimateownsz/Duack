using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins a "shuffle bag": every item is drawn exactly once per cycle (random without replacement),
/// then the cycle refills. Used for playlist shuffle so a track cannot repeat until the others
/// have played.
/// </summary>
public class MarbleBagTests
{
    [Fact]
    public void One_cycle_draws_every_item_exactly_once()
    {
        var items = new[] { "a", "b", "c", "d" };
        var bag = new MarbleBag<string>(items, new Random(1234));

        var drawn = new List<string>();
        for (var i = 0; i < items.Length; i++)
            drawn.Add(bag.Draw());

        Assert.Equal(items.OrderBy(x => x), drawn.OrderBy(x => x));
    }

    [Fact]
    public void Drawing_past_a_full_cycle_refills_and_keeps_drawing()
    {
        var items = new[] { "a", "b", "c" };
        var bag = new MarbleBag<string>(items, new Random(7));

        var drawn = new List<string>();
        for (var i = 0; i < items.Length * 3; i++)
            drawn.Add(bag.Draw());

        Assert.Equal(items.Length * 3, drawn.Count);
        Assert.All(drawn, d => Assert.Contains(d, items));
    }

    [Fact]
    public void A_cycle_is_a_permutation_not_a_repeat_when_Refilled()
    {
        var items = new[] { 1, 2, 3, 4, 5 };
        var bag = new MarbleBag<int>(items, new Random(99));

        var first = Enumerable.Range(0, items.Length).Select(_ => bag.Draw()).ToList();
        var second = Enumerable.Range(0, items.Length).Select(_ => bag.Draw()).ToList();

        Assert.Equal(items.OrderBy(x => x), first.OrderBy(x => x));
        Assert.Equal(items.OrderBy(x => x), second.OrderBy(x => x));
    }

    [Fact]
    public void Same_seed_produces_the_same_draw_sequence()
    {
        var items = new[] { "a", "b", "c", "d", "e" };

        var a = new MarbleBag<string>(items, new Random(2026));
        var b = new MarbleBag<string>(items, new Random(2026));

        var seqA = Enumerable.Range(0, items.Length).Select(_ => a.Draw()).ToList();
        var seqB = Enumerable.Range(0, items.Length).Select(_ => b.Draw()).ToList();

        Assert.Equal(seqA, seqB);
    }

    [Fact]
    public void Remaining_counts_down_and_Refill_resets_it()
    {
        var bag = new MarbleBag<string>(new[] { "a", "b", "c" }, new Random(1));

        Assert.Equal(3, bag.Remaining);

        bag.Draw();
        bag.Draw();
        Assert.Equal(1, bag.Remaining);

        bag.Refill();
        Assert.Equal(3, bag.Remaining);
        Assert.False(bag.IsEmpty);
    }

    [Fact]
    public void A_single_item_bag_always_returns_that_item()
    {
        var bag = new MarbleBag<string>(new[] { "solo" }, new Random(5));

        Assert.Equal("solo", bag.Draw());
        Assert.Equal("solo", bag.Draw());
    }

    [Fact]
    public void An_empty_bag_cannot_draw()
    {
        var bag = new MarbleBag<string>(Array.Empty<string>(), new Random(1));

        Assert.True(bag.IsEmpty);
        Assert.False(bag.TryDraw(out var item));
        Assert.Null(item);
        Assert.Throws<InvalidOperationException>(() => bag.Draw());
    }

    [Fact]
    public void A_null_sequence_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new MarbleBag<string>(null!));
    }
}
