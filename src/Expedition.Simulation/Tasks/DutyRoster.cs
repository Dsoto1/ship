namespace Expedition.Simulation.Tasks;

/// <summary>Why a character is doing a piece of work (TECHNICAL_SPEC.md §19.1).</summary>
public enum TaskOrigin
{
    /// <summary>The player ordered it.</summary>
    Order,

    /// <summary>One of the character's standing duties, taken up because they had nothing else to do.</summary>
    Duty,

    /// <summary>The character looked after themselves, for example resting when tired.</summary>
    SelfCare,

    /// <summary>A local going about their day (R1).</summary>
    Schedule,
}

/// <summary>
/// A character's standing duties, most important first. When idle with no orders they take up the first
/// duty that needs doing and can start, and they leave routine duty work for a more important duty that
/// becomes needed. Player orders always come first. Part of persistent campaign state.
/// </summary>
public sealed class DutyRoster
{
    internal DutyRoster(IReadOnlyList<string> tasks) => Tasks = tasks;

    /// <summary>Task definition IDs, most important first.</summary>
    public IReadOnlyList<string> Tasks { get; }
}
