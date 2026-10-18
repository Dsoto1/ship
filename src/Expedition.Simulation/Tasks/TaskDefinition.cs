using Expedition.Simulation.Characters;

namespace Expedition.Simulation.Tasks;

/// <summary>
/// Immutable description of a kind of work, loaded from <c>data/tasks</c>.
/// Runtime progress is held by <see cref="TaskInstance"/>.
/// </summary>
public sealed record TaskDefinition
{
    /// <summary>Unique definition ID, for example <c>task.pump_bilge</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Work-minutes needed at normal speed (skill level 2, not tired).</summary>
    public required int DurationMinutes { get; init; }

    /// <summary>True for rest. Rest can be started however tired the character is and is not interrupted by fatigue.</summary>
    public bool IsRest { get; init; }

    /// <summary>
    /// True if the task must be done at a work spot that lists it (the pump, the galley). The worker
    /// walks there first. False means the task is done wherever the character stands.
    /// </summary>
    public bool NeedsWorkSpot { get; init; }

    /// <summary>Skill that governs work speed, or null if none applies.</summary>
    public string? Skill { get; init; }

    /// <summary>Minimum level of <see cref="Skill"/> needed to attempt the task.</summary>
    public int MinSkill { get; init; }

    /// <summary>Items that must be on hand but are not used up, keyed by item ID.</summary>
    public IReadOnlyList<string> RequiredTools { get; init; } = [];

    /// <summary>Items used up when the task starts, keyed by item ID. Not refunded if the task is interrupted.</summary>
    public IReadOnlyDictionary<string, int> Consumes { get; init; } = new Dictionary<string, int>();

    /// <summary>Need changes per hour while doing this task, added on top of the baseline.</summary>
    public NeedRates NeedsPerHour { get; init; } = NeedRates.None;

    /// <summary>Risk of injury while working, or null for safe tasks.</summary>
    public TaskHazard? Hazard { get; init; }
}

/// <summary>Chance of injury while doing a task.</summary>
public sealed record TaskHazard
{
    /// <summary>Probability of an injury in each hour of work (0..1).</summary>
    public required double ChancePerHour { get; init; }

    /// <summary>Health lost when the injury happens.</summary>
    public required int HealthDamage { get; init; }

    /// <summary>Condition ID added to the character, for example <c>condition.rope_burn</c>.</summary>
    public required string Condition { get; init; }
}
