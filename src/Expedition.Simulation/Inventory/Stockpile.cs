namespace Expedition.Simulation.Inventory;

/// <summary>
/// Item quantities keyed by item definition ID (for example <c>item.rope</c>).
/// A minimal store for Ticket 2; Ticket 3 adds item definitions, weight, volume and capacity.
/// </summary>
public sealed class Stockpile
{
    private readonly Dictionary<string, int> _quantities = new(StringComparer.Ordinal);

    /// <summary>All items with a quantity above zero.</summary>
    public IReadOnlyDictionary<string, int> Quantities => _quantities;

    /// <summary>Quantity held of <paramref name="itemId"/>, or 0.</summary>
    public int Count(string itemId) => _quantities.GetValueOrDefault(itemId);

    /// <summary>Adds <paramref name="quantity"/> of an item.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The quantity is not positive.</exception>
    public void Add(string itemId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
        }

        _quantities[itemId] = Count(itemId) + quantity;
    }

    /// <summary>True if every item in <paramref name="items"/> is held in at least the given quantity.</summary>
    public bool HasAll(IReadOnlyDictionary<string, int> items) => items.All(pair => Count(pair.Key) >= pair.Value);

    /// <summary>
    /// Removes all of <paramref name="items"/> at once, or nothing if any is short.
    /// Returns false (and changes nothing) when something is missing.
    /// </summary>
    public bool TryRemoveAll(IReadOnlyDictionary<string, int> items)
    {
        if (!HasAll(items))
        {
            return false;
        }

        foreach (var (itemId, quantity) in items)
        {
            var remaining = Count(itemId) - quantity;
            if (remaining == 0)
            {
                _quantities.Remove(itemId);
            }
            else
            {
                _quantities[itemId] = remaining;
            }
        }

        return true;
    }
}
