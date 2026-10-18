using Expedition.Simulation.Core;

namespace Expedition.Simulation.Tasks;

/// <summary>A task a character is currently doing. Part of persistent campaign state.</summary>
public sealed class TaskInstance
{
    /// <summary>Creates a task that has just started.</summary>
    public TaskInstance(CharacterId characterId, string definitionId, long startedAtTick)
    {
        CharacterId = characterId;
        DefinitionId = definitionId;
        StartedAtTick = startedAtTick;
    }

    /// <summary>Who is doing the task.</summary>
    public CharacterId CharacterId { get; }

    /// <summary>The <see cref="TaskDefinition"/> being performed.</summary>
    public string DefinitionId { get; }

    /// <summary>Tick at which the task started.</summary>
    public long StartedAtTick { get; }

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
/// <param name="Reason">For interruptions, why; empty for completions.</param>
public sealed record TaskOutcome(long Tick, CharacterId CharacterId, string DefinitionId, TaskEnd End, string Reason);
