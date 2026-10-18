namespace Expedition.Simulation.History;

/// <summary>Category of a chronicle entry.</summary>
public enum ChronicleEntryKind
{
    /// <summary>The campaign moved to a new phase.</summary>
    PhaseChanged,

    /// <summary>A requested phase transition was not allowed.</summary>
    TransitionRejected,

    /// <summary>A character's survival state changed (injury, death, found, ...).</summary>
    SurvivalStateChanged,

    /// <summary>A character was hurt (for example, by a hazardous task).</summary>
    Injury,

    /// <summary>A character's task stopped before it was finished.</summary>
    TaskInterrupted,

    /// <summary>A task was paused by an urgent order; it waits in the queue with its progress kept.</summary>
    TaskPaused,

    /// <summary>A waiting task could not be started and was removed from the queue.</summary>
    TaskDropped,

    /// <summary>Someone needed food or water and none was available.</summary>
    SupplyShortage,

    /// <summary>A character became the captain the player leads as.</summary>
    CaptainAppointed,

    /// <summary>How one character regards another changed (characters: holder first, then subject).</summary>
    RelationshipChanged,
}
