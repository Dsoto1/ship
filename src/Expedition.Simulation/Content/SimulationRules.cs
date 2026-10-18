using Expedition.Simulation.Characters;
using Expedition.Simulation.Items;

namespace Expedition.Simulation.Content;

/// <summary>Tunable numbers for needs, work and provisions, loaded from <c>data/simulation_rules.json</c>.</summary>
public sealed record SimulationRules
{
    /// <summary>Need changes per hour that apply to every living character, whatever they are doing.</summary>
    public required NeedRates BaselineNeedsPerHour { get; init; }

    /// <summary>Work speed multiplier by skill level, index 0..5. Level 2 (routine competence) should be 1.0.</summary>
    public required IReadOnlyList<double> WorkRateBySkillLevel { get; init; }

    /// <summary>A character at or above this fatigue cannot start work (rest is still allowed).</summary>
    public required double MaxFatigueToStartWork { get; init; }

    /// <summary>Work is interrupted when fatigue reaches this value.</summary>
    public required double ExhaustionFatigue { get; init; }

    /// <summary>At or above this fatigue, work is slowed by <see cref="TiredWorkRateMultiplier"/>.</summary>
    public required double TiredFatigue { get; init; }

    /// <summary>Work speed multiplier for tired characters (0..1).</summary>
    public required double TiredWorkRateMultiplier { get; init; }

    /// <summary>A character whose health falls to or below this becomes incapacitated.</summary>
    public required double IncapacitatedAtHealth { get; init; }

    /// <summary>Characters eat one unit of food when hunger reaches this value.</summary>
    public required double EatAtHunger { get; init; }

    /// <summary>Characters drink one unit when thirst reaches this value.</summary>
    public required double DrinkAtThirst { get; init; }

    /// <summary>Health lost per hour while hunger is at 100.</summary>
    public required double StarvationHealthLossPerHour { get; init; }

    /// <summary>Health lost per hour while thirst is at 100.</summary>
    public required double DehydrationHealthLossPerHour { get; init; }

    /// <summary>Grid cells a character walks per in-game minute. Cells are about one metre.</summary>
    public required double WalkCellsPerMinute { get; init; }

    /// <summary>How much one person can carry. Applied to personal inventories created by scenarios.</summary>
    public required InventoryCapacity PersonalCarryCapacity { get; init; }
}
