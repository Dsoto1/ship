namespace Expedition.Simulation.Items;

/// <summary>Limits on what an inventory can hold. A null limit means that dimension is unlimited.</summary>
public sealed record InventoryCapacity
{
    /// <summary>No limits at all.</summary>
    public static InventoryCapacity Unlimited { get; } = new();

    /// <summary>Maximum total weight in kilograms, or null for no limit.</summary>
    public double? MaxWeightKg { get; init; }

    /// <summary>Maximum total volume in litres, or null for no limit.</summary>
    public double? MaxVolumeLitres { get; init; }
}
