namespace Expedition.Simulation.Characters;

/// <summary>
/// Change in each need per in-game hour. Positive values make the need worse (more hungry, more tired);
/// negative values relieve it.
/// </summary>
public sealed record NeedRates
{
    /// <summary>No change to any need.</summary>
    public static NeedRates None { get; } = new();

    /// <summary>Hunger change per hour.</summary>
    public double Hunger { get; init; }

    /// <summary>Thirst change per hour.</summary>
    public double Thirst { get; init; }

    /// <summary>Fatigue change per hour.</summary>
    public double Fatigue { get; init; }

    /// <summary>Stress change per hour.</summary>
    public double Stress { get; init; }

    /// <summary>Sums two sets of rates, for example a baseline plus an activity.</summary>
    public static NeedRates operator +(NeedRates a, NeedRates b) => new()
    {
        Hunger = a.Hunger + b.Hunger,
        Thirst = a.Thirst + b.Thirst,
        Fatigue = a.Fatigue + b.Fatigue,
        Stress = a.Stress + b.Stress,
    };
}
