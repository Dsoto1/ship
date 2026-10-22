using Expedition.Simulation.Core;

namespace Expedition.Simulation.Items;

/// <summary>
/// Whole-unit item quantities in one place: a character's pack, a ship's hold, a camp store.
/// Adding checks item IDs and capacity; removal is all-or-nothing.
/// Items move between inventories with <see cref="TryTransfer"/>, which never creates or destroys items.
/// </summary>
public sealed class Inventory
{
    // Absorbs rounding when weights such as 0.1 kg are summed.
    private const double CapacityTolerance = 1e-9;

    private readonly SortedDictionary<string, int> _quantities = new(StringComparer.Ordinal);

    /// <summary>Creates an empty inventory.</summary>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    public Inventory(InventoryId id, string name, InventoryCapacity capacity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Inventory name must not be empty.", nameof(name));
        }

        Id = id;
        Name = name;
        Capacity = capacity;
    }

    /// <summary>Stable identifier.</summary>
    public InventoryId Id { get; }

    /// <summary>Display name, for example "Ship's hold".</summary>
    public string Name { get; }

    /// <summary>Weight and volume limits.</summary>
    public InventoryCapacity Capacity { get; internal set; }

    /// <summary>All items held, by item ID, in ID order. Quantities are always positive.</summary>
    public IReadOnlyDictionary<string, int> Quantities => _quantities;

    /// <summary>Quantity held of <paramref name="itemId"/>, or 0.</summary>
    public int Count(string itemId) => _quantities.GetValueOrDefault(itemId);

    /// <summary>Total weight of everything held, in kilograms.</summary>
    public double TotalWeightKg(IReadOnlyDictionary<string, ItemDefinition> items) =>
        _quantities.Sum(pair => items[pair.Key].WeightKg * pair.Value);

    /// <summary>Total volume of everything held, in litres.</summary>
    public double TotalVolumeLitres(IReadOnlyDictionary<string, ItemDefinition> items) =>
        _quantities.Sum(pair => items[pair.Key].VolumeLitres * pair.Value);

    /// <summary>Checks whether <paramref name="quantity"/> of an item would fit, without changing anything.</summary>
    public CommandResult CanAdd(string itemId, int quantity, IReadOnlyDictionary<string, ItemDefinition> items)
    {
        if (quantity <= 0)
        {
            return CommandResult.Rejected("Quantity must be positive.");
        }

        if (!items.TryGetValue(itemId, out var item))
        {
            return CommandResult.Rejected($"Unknown item '{itemId}'.");
        }

        if (Capacity.MaxWeightKg is { } maxWeight)
        {
            var weight = TotalWeightKg(items) + item.WeightKg * quantity;
            if (weight > maxWeight + CapacityTolerance)
            {
                return CommandResult.Rejected($"{Name} cannot take {quantity} {item.Name}: {weight:0.##} kg exceeds {maxWeight:0.##} kg.");
            }
        }

        if (Capacity.MaxVolumeLitres is { } maxVolume)
        {
            var volume = TotalVolumeLitres(items) + item.VolumeLitres * quantity;
            if (volume > maxVolume + CapacityTolerance)
            {
                return CommandResult.Rejected($"{Name} cannot take {quantity} {item.Name}: {volume:0.##} L exceeds {maxVolume:0.##} L.");
            }
        }

        return CommandResult.Ok();
    }

    /// <summary>Adds items if they exist and fit; otherwise changes nothing and says why.</summary>
    public CommandResult TryAdd(string itemId, int quantity, IReadOnlyDictionary<string, ItemDefinition> items)
    {
        var check = CanAdd(itemId, quantity, items);
        if (check.Succeeded)
        {
            _quantities[itemId] = Count(itemId) + quantity;
        }

        return check;
    }

    /// <summary>True if every item in <paramref name="wanted"/> is held in at least the given quantity.</summary>
    public bool HasAll(IReadOnlyDictionary<string, int> wanted) => wanted.All(pair => Count(pair.Key) >= pair.Value);

    /// <summary>Removes <paramref name="quantity"/> of an item, or nothing if there is not enough.</summary>
    public bool TryRemove(string itemId, int quantity)
    {
        if (quantity <= 0 || Count(itemId) < quantity)
        {
            return false;
        }

        var remaining = Count(itemId) - quantity;
        if (remaining == 0)
        {
            _quantities.Remove(itemId);
        }
        else
        {
            _quantities[itemId] = remaining;
        }

        return true;
    }

    /// <summary>
    /// Moves items from <paramref name="source"/> to <paramref name="destination"/>. Either the whole
    /// quantity moves or nothing does, so the total number of items never changes.
    /// </summary>
    public static CommandResult TryTransfer(
        Inventory source, Inventory destination, string itemId, int quantity, IReadOnlyDictionary<string, ItemDefinition> items)
    {
        if (ReferenceEquals(source, destination))
        {
            return CommandResult.Rejected("Source and destination are the same inventory.");
        }

        if (quantity <= 0 || source.Count(itemId) < quantity)
        {
            return CommandResult.Rejected($"{source.Name} does not hold {quantity} of {itemId}.");
        }

        var fits = destination.TryAdd(itemId, quantity, items);
        if (fits.Succeeded)
        {
            source.TryRemove(itemId, quantity);
        }

        return fits;
    }

    /// <summary>Sets a quantity directly while loading a save. Capacity is not checked: content tuning must not make old saves unloadable.</summary>
    internal void Restore(string itemId, int quantity) => _quantities[itemId] = quantity;
}
