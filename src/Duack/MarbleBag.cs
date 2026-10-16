namespace Duack;

/// <summary>
/// A "shuffle bag": items are drawn in random order without replacement, so every item appears
/// exactly once per cycle, then the cycle refills. Generic over the item type, so it serves both
/// halves of the audio system: a music playlist (a track cannot repeat until all the others have
/// played) and a sound-effect variant pool (footstep/hit variants do not repeat back-to-back).
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
public sealed class MarbleBag<T>
{
    private readonly List<T> _items;
    private readonly Random _random;
    private readonly List<T> _bag;
    private int _next;

    /// <param name="items">The set drawn from. Duplicates are allowed and counted separately.</param>
    /// <param name="random">Source of randomness; defaults to <see cref="Random.Shared"/>.</param>
    public MarbleBag(IEnumerable<T> items, Random? random = null)
    {
        _items = items?.ToList() ?? throw new ArgumentNullException(nameof(items));
        _random = random ?? Random.Shared;
        _bag = new List<T>(_items.Count);
        Refill();
    }

    /// <summary>Number of items in one full cycle.</summary>
    public int Count => _items.Count;

    /// <summary>Number of items left to draw before the next refill.</summary>
    public int Remaining => _items.Count - _next;

    /// <summary>True when the bag has no items to draw at all.</summary>
    public bool IsEmpty => _items.Count == 0;

    /// <summary>Reshuffles a fresh cycle containing every item once.</summary>
    public void Refill()
    {
        _bag.Clear();
        _bag.AddRange(_items);

        for (var i = _bag.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
        }

        _next = 0;
    }

    /// <summary>
    /// Draws the next item, refilling automatically when the current cycle is exhausted.
    /// </summary>
    /// <exception cref="InvalidOperationException">The bag was constructed with no items.</exception>
    public T Draw()
    {
        if (_items.Count == 0)
            throw new InvalidOperationException("Cannot draw from an empty marble bag.");

        if (_next >= _bag.Count)
            Refill();

        return _bag[_next++];
    }

    /// <summary>
    /// Draws the next item, or returns false when the bag was constructed with no items.
    /// </summary>
    public bool TryDraw(out T item)
    {
        if (_items.Count == 0)
        {
            item = default!;
            return false;
        }

        item = Draw();
        return true;
    }
}
