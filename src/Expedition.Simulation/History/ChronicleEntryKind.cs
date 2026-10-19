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

    /// <summary>The weather at sea changed (parameters: from, to).</summary>
    WeatherChanged,

    /// <summary>A heavy sea struck the hull and opened a seam (parameters: damage, level = hull now).</summary>
    HullStruck,

    /// <summary>The bilge rose past an alarm level (parameter: level).</summary>
    BilgeAlarm,

    /// <summary>The ship filled and sank.</summary>
    Foundered,

    /// <summary>The destination was sighted (parameter: to = destination).</summary>
    Landfall,

    /// <summary>The captain decided an event (parameters: event, choice, outcome; characters involved).</summary>
    EventResolved,

    /// <summary>Rations were cut or restored (parameter: to = short or full).</summary>
    RationingChanged,

    /// <summary>The captain changed how much sail is carried (parameter: to = plan name).</summary>
    SailPlanChanged,

    /// <summary>Nobody is left alive aboard.</summary>
    AllHandsLost,

    /// <summary>Someone signed on (parameter: level = advance paid; character: who).</summary>
    CrewHired,
}
