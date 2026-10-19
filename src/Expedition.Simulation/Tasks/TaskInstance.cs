using Expedition.Simulation.Core;

namespace Expedition.Simulation.Tasks;

/// <summary>Stable identifier for one piece of ordered work, unique within a campaign.</summary>
/// <param name="Value">Positive number issued by <see cref="IdAllocator"/>.</param>
public readonly record struct TaskId(int Value)
{
    /// <summary>Readable form such as <c>work-0012</c>.</summary>
    public override string ToString() => $"work-{Value:D4}";
}

/// <summary>How urgent an order is (TECHNICAL_SPEC.md §7.3).</summary>
public enum TaskPriority
{
    /// <summary>Ordinary work, done in the order it was given.</summary>
    Normal,

    /// <summary>Emergency work. It pauses normal work, which resumes afterwards with its progress kept.</summary>
    Urgent,
}

/// <summary>
/// One piece of ordered work for one character: either the task they are doing now
/// (<see cref="Campaign.CampaignState.ActiveTasks"/>) or one waiting in their queue
/// (<see cref="Campaign.CampaignState.QueueOf"/>). A paused task keeps its progress and the
/// materials it already used. Part of persistent campaign state.
/// </summary>
public sealed class TaskInstance
{
    /// <summary>Creates an order that has not started yet.</summary>
    public TaskInstance(TaskId id, CharacterId characterId, string definitionId, TaskPriority priority)
    {
        Id = id;
        CharacterId = characterId;
        DefinitionId = definitionId;
        Priority = priority;
    }

    /// <summary>Stable ID of this piece of work.</summary>
    public TaskId Id { get; }

    /// <summary>Who is to do the task.</summary>
    public CharacterId CharacterId { get; }

    /// <summary>The <see cref="TaskDefinition"/> to perform.</summary>
    public string DefinitionId { get; }

    /// <summary>How urgent the order is.</summary>
    public TaskPriority Priority { get; }

    /// <summary>Why the character is doing it: a player order, a standing duty or self-care.</summary>
    public TaskOrigin Origin { get; init; } = TaskOrigin.Order;

    /// <summary>Tick at which work first started, or null if it has not started yet.</summary>
    public long? StartedAtTick { get; internal set; }

    /// <summary>True once the task's materials have been used. They are never used twice, even after a pause.</summary>
    public bool MaterialsConsumed { get; internal set; }

    /// <summary>The work spot reserved while the task is active, or null (tasks done anywhere, or waiting).</summary>
    public string? SpotId { get; internal set; }

    /// <summary>Work-minutes completed so far. The task finishes when this reaches the definition's duration.</summary>
    public double Progress { get; internal set; }
}

/// <summary>How a task ended.</summary>
public enum TaskEnd
{
    /// <summary>The work was finished.</summary>
    Completed,

    /// <summary>The work stopped before it was finished.</summary>
    Interrupted,
}

/// <summary>Report of a task that ended during <see cref="World.SimulationRunner.Advance"/>.</summary>
/// <param name="Tick">Tick at which it ended.</param>
/// <param name="CharacterId">Who was doing it.</param>
/// <param name="DefinitionId">Which task.</param>
/// <param name="End">Completed or interrupted.</param>
/// <param name="Reason">For interruptions, one of <see cref="History.InterruptReasons"/>; empty for completions.</param>
public sealed record TaskOutcome(long Tick, CharacterId CharacterId, string DefinitionId, TaskEnd End, string Reason);
