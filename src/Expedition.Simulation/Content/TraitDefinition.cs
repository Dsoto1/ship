namespace Expedition.Simulation.Content;

/// <summary>
/// A personality trait, loaded from <c>data/traits.json</c>. Traits are meant to change behaviour, not
/// just numbers (TECHNICAL_SPEC.md §5.5); the behaviours themselves arrive with the systems that use them
/// (orders, recruitment, events). For now a trait is a validated, named fact about a person.
/// </summary>
public sealed record TraitDefinition
{
    /// <summary>Unique ID, for example <c>trait.cautious</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>What the trait means for how the person behaves.</summary>
    public required string Description { get; init; }
}

/// <summary>An injury, illness or other health condition, loaded from <c>data/conditions.json</c>.</summary>
public sealed record ConditionDefinition
{
    /// <summary>Unique ID, for example <c>condition.rope_burn</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Broad kind: <c>injury</c> or <c>illness</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>An injury: about how many days it takes to heal, resting and tended (longer at work, untended).</summary>
    public double HealDays { get; init; } = 4;

    /// <summary>An illness: the chance each hour of getting over it, before rest and care.</summary>
    public double RecoveryPerHour { get; init; } = 1.0 / 60;

    /// <summary>An illness: health it wears away each hour.</summary>
    public double HealthPerHour { get; init; } = 0.25;

    /// <summary>An illness: the chance each hour that it passes from one sick person to each shipmate, in a clean ship.</summary>
    public double Contagion { get; init; }

    /// <summary>How fast someone with it works: 0.6 is at six-tenths of their usual pace.</summary>
    public double WorkRate { get; init; } = 1;

    /// <summary>An illness only fresh food cures (scurvy): it does not pass on its own.</summary>
    public bool CuredByFresh { get; init; }
}
