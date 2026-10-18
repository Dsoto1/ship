namespace Expedition.Simulation.Items;

/// <summary>A kind of item, loaded from <c>data/items.json</c>. Quantities are whole units.</summary>
public sealed record ItemDefinition
{
    /// <summary>Unique ID, for example <c>item.biscuit</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Broad category, for example <c>food</c>, <c>water</c>, <c>tool</c>, <c>material</c>.</summary>
    public required string Category { get; init; }

    /// <summary>Weight of one unit in kilograms.</summary>
    public required double WeightKg { get; init; }

    /// <summary>Space one unit takes up, in litres.</summary>
    public required double VolumeLitres { get; init; }

    /// <summary>Hunger removed by eating one unit. Zero for things that are not food.</summary>
    public double HungerRelief { get; init; }

    /// <summary>Thirst removed by drinking one unit. Zero for things that are not drink.</summary>
    public double ThirstRelief { get; init; }
}
