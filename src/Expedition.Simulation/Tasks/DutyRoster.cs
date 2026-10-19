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
}

/// <summary>
/// A character's standing duties: tasks they take up in turn whenever they are idle and have no orders.
/// Player orders always come first, and a player order pre-empts duty work (which resumes afterwards).
/// Part of persistent campaign state.
/// </summary>
public sealed class DutyRoster
{
    internal DutyRoster(IReadOnlyList<string> tasks, int next = 0)
    {
        Tasks = tasks;
        Next = next;
    }

    /// <summary>Task definition IDs, in the order they are taken up.</summary>
    public IReadOnlyList<string> Tasks { get; }

    /// <summary>Index of the duty to try first next time, so duties are done in turn.</summary>
    public int Next { get; internal set; }
}
