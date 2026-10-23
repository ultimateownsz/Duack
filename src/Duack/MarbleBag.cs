namespace Duack;

/// <summary>
/// A "shuffle bag": items are drawn in random order without replacement, so every item appears
/// exactly once per cycle, then the cycle refills. Generic over the item type, so it serves both
/// halves of the audio system: a music playlist (a track cannot repeat until all the others have
/// played) and a sound-effect variant pool (footstep/hit variants do not repeat back-to-back).
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
public class MarbleBag<T>
{
    private readonly List<T> _items;
    private readonly Random _random;
    private readonly List<T> _bag;
    private readonly bool _avoidImmediateRepeat;
    private int _next;
    private bool _hasDrawn;
    private T _lastDrawn = default!;

    /// <param name="items">The set drawn from. Duplicates are allowed and counted separately.</param>
    /// <param name="random">Source of randomness; defaults to <see cref="Random.Shared"/>.</param>
    /// <param name="avoidImmediateRepeat">
    /// When true, a draw never returns the same item as the previous draw, including across a cycle
    /// boundary (the last item of one cycle differs from the first of the next), so a shuffled
    /// playlist loops without a back-to-back repeat at the seam. Best effort with duplicate items:
    /// it needs at least one item different from the last draw. Default is false (plain shuffle-bag).
    /// </param>
    public MarbleBag(IEnumerable<T> items, Random? random = null, bool avoidImmediateRepeat = false)
    {
        _items = items?.ToList() ?? throw new ArgumentNullException(nameof(items));
        _random = random ?? Random.Shared;
        _avoidImmediateRepeat = avoidImmediateRepeat;
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

        if (_avoidImmediateRepeat &&
            _hasDrawn &&
            _items.Count > 1 &&
            EqualityComparer<T>.Default.Equals(_bag[_next], _lastDrawn))
        {
            var swap = _next + 1 < _bag.Count ? _next + 1 : 0;
            (_bag[_next], _bag[swap]) = (_bag[swap], _bag[_next]);
        }

        _lastDrawn = _bag[_next];
        _hasDrawn = true;
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
